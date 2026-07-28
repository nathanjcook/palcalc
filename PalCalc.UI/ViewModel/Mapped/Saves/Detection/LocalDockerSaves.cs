using CommunityToolkit.Mvvm.Input;
using PalCalc.SaveReader;
using PalCalc.UI.Localization;
using PalCalc.UI.Model;
using PalCalc.UI.Model.Service;
using PalCalc.UI.ViewModel.SaveSelection;
using Serilog;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;

using AdonisMessageBox = AdonisUI.Controls.MessageBox;

namespace PalCalc.UI.ViewModel.Mapped.Saves.Detection
{
    // Auto-detects Palworld dedicated-server saves running in LOCAL Docker (e.g. Docker Desktop with
    // the thijsvanloef image). Resolves each Palworld container's bind-mount host path via the
    // `docker` CLI, then reads the save with the existing StandardSaveGame parser — same as a manual
    // local save, just found automatically. Best-effort: containers whose save lives in a named
    // volume / inside the Docker VM (no host path reachable from Windows) are skipped.
    internal static class LocalDockerSaves
    {
        private static readonly ILogger logger = Log.ForContext(typeof(LocalDockerSaves));

        public static SavesCollectionViewModel CollectAll(AppSettings settings, ISavesService savesService)
        {
            List<StandardSaveGame> saves;
            try
            {
                saves = DockerSaveLocator.LocateSaveFolders()
                    .Select(dir => new StandardSaveGame(dir))
                    .Where(sg => sg.IsValid)
                    .ToList();
            }
            catch (Exception e)
            {
                logger.Warning(e, "Local Docker save detection failed");
                saves = new();
            }

            return FromList(saves);
        }

        public static SaveGameViewModel FromSave(SavesCollectionViewModel parent, StandardSaveGame save)
        {
            var res = SavesCommon.BuildNormalSave(
                parent: parent,
                save: save,
                openFolderCommand: new RelayCommand(() => WindowsUtils.OpenPathInExplorer(save.BasePath))
            );

            res.Type = SaveType.LocalDocker;

            return res;
        }

        public static SavesCollectionViewModel FromList(IEnumerable<StandardSaveGame> saves)
        {
            var res = new SavesCollectionViewModel()
            {
                SaveType = SaveType.LocalDocker,
                TypeLabel = new HardCodedText("Local Docker"),
                Title = null,
                OpenFolderCommand = null,
            };

            var availableSaves = new ObservableCollection<SaveGameViewModel>([
                .. saves.Select(sg => FromSave(res, sg)).OrderBy(sg => sg.CombinedLabel.Value)
            ]);
            res.AvailableSaves = new(availableSaves);

            // These are auto-detected (not persisted), so "Add" just re-scans running containers —
            // useful if Docker wasn't up at launch or a container started since.
            res.AddSaveCommand = new RelayCommand(() =>
            {
                List<StandardSaveGame> found;
                try
                {
                    found = DockerSaveLocator.LocateSaveFolders()
                        .Select(d => new StandardSaveGame(d))
                        .Where(s => s.IsValid)
                        .ToList();
                }
                catch (Exception ex)
                {
                    AdonisMessageBox.Show(App.Current.MainWindow, "Docker scan failed:\n\n" + ex.Message, caption: "");
                    return;
                }

                foreach (var sg in found)
                {
                    if (availableSaves.Any(existing => existing.Value.BasePath == sg.BasePath))
                    {
                        sg.Dispose();
                        continue;
                    }

                    var vm = FromSave(res, sg);
                    // insert while preserving alphanumeric ordering
                    var orderedIndex = availableSaves
                        .AsEnumerable()
                        .Append(vm)
                        .OrderBy(x => x.CombinedLabel.Value)
                        .ToList()
                        .IndexOf(vm);
                    availableSaves.Insert(orderedIndex, vm);
                }
            });

            res.RemoveSaveCommand = new RelayCommand<SaveGameViewModel>((save) =>
            {
                // Transient (re-detected each launch) — just drop it from the list, don't touch disk.
                availableSaves.Remove(save);
                (save.Value as StandardSaveGame)?.Dispose();
            });

            return res;
        }
    }

    // Resolves local Palworld world-save folders by inspecting running Docker containers via the
    // `docker` CLI. No SDK dependency; shells out and parses the output. Windows-oriented (Docker
    // Desktop), but the CLI calls are platform-neutral.
    internal static class DockerSaveLocator
    {
        private static readonly ILogger logger = Log.ForContext(typeof(DockerSaveLocator));

        // Host subpaths, relative to a container bind-mount source, under which a Palworld world save
        // ("<world-id>/Level.sav") is typically found. thijsvanloef mounts the game dir, so the save
        // sits under Pal/Saved/SaveGames/0. Ordered most- to least-specific; "" covers a mount made
        // directly at the SaveGames/0 (or world) folder.
        private static readonly string[] SaveGamesSubPaths =
        {
            Path.Combine("Pal", "Saved", "SaveGames", "0"),
            Path.Combine("Saved", "SaveGames", "0"),
            Path.Combine("SaveGames", "0"),
            "0",
            "",
        };

        public static IEnumerable<string> LocateSaveFolders()
        {
            var results = new List<string>();

            var ps = RunDocker("ps --no-trunc --format \"{{.ID}}|{{.Image}}|{{.Names}}\"", 5000);
            if (ps == null) return results; // docker not installed / daemon down

            foreach (var line in ps.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var parts = line.Split('|');
                if (parts.Length < 3) continue;

                var id = parts[0];
                var image = parts[1];
                var name = parts[2];

                // Palworld dedicated-server containers (thijsvanloef image is
                // "thijsvanloef/palworld-server-docker", but match broadly on image or name).
                if (!image.Contains("palworld", StringComparison.OrdinalIgnoreCase)
                    && !name.Contains("palworld", StringComparison.OrdinalIgnoreCase))
                    continue;

                var mounts = RunDocker($"inspect --format \"{{{{range .Mounts}}}}{{{{.Source}}}}\\n{{{{end}}}}\" {id}", 5000);
                if (mounts == null) continue;

                foreach (var rawSource in mounts.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    foreach (var hostRoot in HostCandidates(rawSource))
                    {
                        foreach (var sub in SaveGamesSubPaths)
                        {
                            var candidate = sub.Length == 0 ? hostRoot : Path.Combine(hostRoot, sub);
                            foreach (var world in WorldSavesUnder(candidate))
                            {
                                if (!results.Contains(world)) results.Add(world);
                            }
                        }
                    }
                }
            }

            return results;
        }

        // World-save folders at/under `dir`: the dir itself if it holds a Level.sav, plus any
        // immediate subfolder that does. Covers a mount at the world folder or at the SaveGames/0 dir.
        private static IEnumerable<string> WorldSavesUnder(string dir)
        {
            if (!SafeDirExists(dir)) yield break;

            if (SafeFileExists(Path.Combine(dir, "Level.sav"))) yield return dir;

            foreach (var sub in SafeEnumerateDirs(dir))
                if (SafeFileExists(Path.Combine(sub, "Level.sav"))) yield return sub;
        }

        // A bind-mount source as reported by `docker inspect`, plus a Windows-path translation.
        // Docker Desktop (WSL2) often reports sources as Linux VM passthrough paths.
        private static IEnumerable<string> HostCandidates(string source)
        {
            if (string.IsNullOrWhiteSpace(source)) yield break;

            yield return source; // bind mount to a Windows folder is reported as-is

            var translated = TranslateDockerDesktopPath(source);
            if (translated != null && !string.Equals(translated, source, StringComparison.OrdinalIgnoreCase))
                yield return translated;
        }

        //   /host_mnt/c/foo               -> C:\foo
        //   /run/desktop/mnt/host/c/foo   -> C:\foo
        //   /mnt/c/foo                    -> C:\foo
        private static string TranslateDockerDesktopPath(string p)
        {
            p = p.Replace('\\', '/');
            string[] prefixes = { "/run/desktop/mnt/host/", "/host_mnt/", "/mnt/" };
            foreach (var pref in prefixes)
            {
                if (p.StartsWith(pref, StringComparison.OrdinalIgnoreCase))
                {
                    var rest = p.Substring(pref.Length); // "c/foo/bar"
                    if (rest.Length >= 2 && rest[1] == '/')
                    {
                        var drive = char.ToUpperInvariant(rest[0]);
                        var tail = rest.Substring(2).Replace('/', '\\');
                        return $"{drive}:\\{tail}";
                    }
                }
            }
            return null;
        }

        private static string RunDocker(string args, int timeoutMs)
        {
            try
            {
                var psi = new ProcessStartInfo("docker", args)
                {
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                };

                using var proc = Process.Start(psi);
                if (proc == null) return null;

                // Drain stderr async so a chatty child can't deadlock the stdout read.
                var errTask = proc.StandardError.ReadToEndAsync();
                var stdout = proc.StandardOutput.ReadToEnd();

                if (!proc.WaitForExit(timeoutMs))
                {
                    try { proc.Kill(true); } catch { /* best effort */ }
                    return null;
                }

                return proc.ExitCode == 0 ? stdout : null;
            }
            catch (Exception e)
            {
                // docker not installed / not on PATH / daemon unreachable
                logger.Debug(e, "docker {Args} failed", args);
                return null;
            }
        }

        private static bool SafeDirExists(string p) { try { return Directory.Exists(p); } catch { return false; } }
        private static bool SafeFileExists(string p) { try { return File.Exists(p); } catch { return false; } }
        private static IEnumerable<string> SafeEnumerateDirs(string p)
        {
            try { return Directory.EnumerateDirectories(p); } catch { return Array.Empty<string>(); }
        }
    }
}
