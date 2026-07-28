using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using PalCalc.SaveReader;
using PalCalc.UI.Localization;
using PalCalc.UI.Model;
using PalCalc.UI.Model.Service;
using PalCalc.UI.View.Utils;
using PalCalc.UI.ViewModel.SaveSelection;
using Serilog;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

using AdonisMessageBox = AdonisUI.Controls.MessageBox;
using AdonisMessageBoxButton = AdonisUI.Controls.MessageBoxButton;
using AdonisMessageBoxResult = AdonisUI.Controls.MessageBoxResult;

namespace PalCalc.UI.ViewModel.Mapped.Saves.Detection
{
    // Remote dedicated-server saves fetched over SFTP. The save is downloaded into a local cache
    // dir and then read by the existing StandardSaveGame parser (dedicated-server saves already
    // parse). See RemoteSaveFetcher / RemoteSaveConnection in PalCalc.SaveReader.
    internal static class DedicatedServerSaves
    {
        private static readonly ILogger logger = Log.ForContext(typeof(DedicatedServerSaves));

        public static SavesCollectionViewModel CollectAll(AppSettings settings, ISavesService savesService)
        {
            var loaded = new List<StandardSaveGame>();
            foreach (var conn in settings.RemoteSaveLocations)
            {
                try
                {
                    var dir = RemoteSaveFetcher.Fetch(conn);
                    loaded.Add(new StandardSaveGame(dir));
                }
                catch (Exception e)
                {
                    // Server unreachable / auth failure: skip so it doesn't block startup.
                    logger.Warning(e, "Failed to fetch remote save {user}@{host}", conn.Username, conn.Host);
                }
            }

            return FromList(settings, loaded, savesService);
        }

        public static SaveGameViewModel FromSave(SavesCollectionViewModel parent, StandardSaveGame save)
        {
            // BuildNormalSave already renders the server-save label (world name + day).
            var res = SavesCommon.BuildNormalSave(parent, save, openFolderCommand: null);
            res.Type = SaveType.DedicatedServer;
            return res;
        }

        public static SavesCollectionViewModel FromList(AppSettings settings, IEnumerable<StandardSaveGame> existingSaves, ISavesService savesService)
        {
            var res = new SavesCollectionViewModel()
            {
                SaveType = SaveType.DedicatedServer,
                TypeLabel = new HardCodedText("Dedicated Server"),
                Title = null,
                OpenFolderCommand = null,
            };

            var availableSaves = new ObservableCollection<SaveGameViewModel>([
                .. existingSaves.Select(sg => FromSave(res, sg)).OrderBy(sg => sg.CombinedLabel.Value)
            ]);
            res.AvailableSaves = new(availableSaves);

            res.AddSaveCommand = new AsyncRelayCommand(async () =>
            {
                // 1) SSH connection string
                var connWindow = new SimpleTextInputWindow()
                {
                    Title = "Add Dedicated Server Save",
                    InputLabel = "SSH connection: user@host[:port]:/path/to/SaveGames/0/<world-id>",
                    Validator = s => s != null && s.Contains(":/"),
                    Owner = App.ActiveWindow,
                };
                if (connWindow.ShowDialog() != true) return;

                // 2) auth: SSH key (recommended) or password
                var useKey = AdonisMessageBox.Show(
                    App.ActiveWindow,
                    "Authenticate with an SSH key file?\n\nChoose No to use a password instead (stored in plaintext in settings).",
                    "SSH Authentication",
                    AdonisMessageBoxButton.YesNo
                ) == AdonisMessageBoxResult.Yes;

                string keyPath = null, password = null;
                if (useKey)
                {
                    var ofd = new OpenFileDialog() { Title = "Select the SSH private key to connect with" };
                    if (ofd.ShowDialog(App.Current.MainWindow) != true) return;
                    keyPath = ofd.FileName;
                }
                else
                {
                    var pwWindow = new SimpleTextInputWindow()
                    {
                        Title = "SSH Password",
                        InputLabel = "Password (stored in plaintext in settings)",
                        Validator = s => s != null && s.Length > 0,
                        Owner = App.ActiveWindow,
                    };
                    if (pwWindow.ShowDialog() != true) return;
                    password = pwWindow.Result;
                }

                RemoteSaveConnection conn;
                try
                {
                    conn = RemoteSaveConnection.Parse(connWindow.Result, keyPath);
                    conn.Password = password;
                }
                catch (Exception ex)
                {
                    AdonisMessageBox.Show(App.Current.MainWindow, ex.Message, caption: "");
                    return;
                }

                if (settings.RemoteSaveLocations.Any(c => c.Id == conn.Id))
                {
                    AdonisMessageBox.Show(App.Current.MainWindow, "That server save is already registered.", caption: "");
                    return;
                }

                StandardSaveGame save;
                try
                {
                    // async so the UI stays responsive while connecting / downloading
                    var dir = await RemoteSaveFetcher.FetchAsync(conn);
                    save = new StandardSaveGame(dir);
                }
                catch (Exception ex)
                {
                    AdonisMessageBox.Show(App.Current.MainWindow, "Failed to connect / download the save:\n\n" + ex.Message, caption: "");
                    return;
                }

                if (!save.IsValid)
                {
                    save.Dispose();
                    AdonisMessageBox.Show(App.Current.MainWindow, "No valid Level.sav was found at that path.", caption: "");
                    return;
                }

                savesService.AddRemoteSave(conn);

                var vm = FromSave(res, save);
                var orderedIndex = availableSaves
                    .AsEnumerable()
                    .Append(vm)
                    .OrderBy(x => x.CombinedLabel.Value)
                    .ToList()
                    .IndexOf(vm);
                availableSaves.Insert(orderedIndex, vm);
            });

            res.RemoveSaveCommand = new RelayCommand<SaveGameViewModel>((save) =>
            {
                var confirmation = AdonisMessageBox.Show(
                    App.ActiveWindow,
                    LocalizationCodes.LC_REMOVE_SAVE_DESCRIPTION.Bind(save.CombinedLabel).Value,
                    LocalizationCodes.LC_REMOVE_SAVE_TITLE.Bind().Value,
                    AdonisMessageBoxButton.YesNo
                );

                if (confirmation != AdonisMessageBoxResult.Yes) return;

                availableSaves.Remove(save);

                var sg = save.Value as StandardSaveGame;
                var conn = settings.RemoteSaveLocations.FirstOrDefault(c => c.LocalCacheDir == sg?.BasePath);
                if (conn != null) savesService.RemoveRemoteSave(conn);

                sg?.Dispose();
            });

            return res;
        }
    }
}
