using Renci.SshNet;
using Serilog;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace PalCalc.SaveReader
{
    /// <summary>
    /// Downloads a dedicated-server save over SFTP into the connection's local cache dir, so the
    /// existing folder-based parser (<see cref="StandardSaveGame"/>) can read it. The only new
    /// concept vs a local save is this fetch step.
    /// </summary>
    public static class RemoteSaveFetcher
    {
        private static readonly ILogger logger = Log.ForContext(typeof(RemoteSaveFetcher));

        private const int MaxAttempts = 3;
        private static readonly TimeSpan RetryDelay = TimeSpan.FromMilliseconds(500);

        // Top-level save files to mirror (WorldOption/LocalData may be absent on linux server saves).
        private static readonly string[] TopLevelFiles =
            ["Level.sav", "LevelMeta.sav", "LocalData.sav", "WorldOption.sav"];

        /// <summary>
        /// Fetch the save into <see cref="RemoteSaveConnection.LocalCacheDir"/> and return that path.
        /// Retries because the server rewrites Level.sav periodically — a mid-write pull can be
        /// partial/invalid. Throws on connection/auth failure (callers should catch and surface).
        /// </summary>
        public static string Fetch(RemoteSaveConnection conn)
        {
            Exception lastError = null;

            for (var attempt = 1; attempt <= MaxAttempts; attempt++)
            {
                try
                {
                    var dir = FetchOnce(conn);

                    // Guard against a partial Level.sav from a concurrent server save-write.
                    using var check = new StandardSaveGame(dir);
                    if (check.IsValid)
                        return dir;

                    logger.Warning("Remote save invalid after fetch (attempt {n}/{max})", attempt, MaxAttempts);
                }
                catch (Exception e)
                {
                    lastError = e;
                    logger.Warning(e, "Remote fetch failed (attempt {n}/{max})", attempt, MaxAttempts);
                }

                if (attempt < MaxAttempts)
                    Thread.Sleep(RetryDelay);
            }

            if (lastError != null)
                throw lastError;

            // Downloaded but never validated — return the dir; the caller's IsValid check surfaces it.
            return conn.LocalCacheDir;
        }

        public static Task<string> FetchAsync(RemoteSaveConnection conn) => Task.Run(() => Fetch(conn));

        private static string FetchOnce(RemoteSaveConnection conn)
        {
            var localDir = conn.LocalCacheDir;
            Directory.CreateDirectory(localDir);
            Directory.CreateDirectory(Path.Join(localDir, "Players"));

            var remote = conn.RemoteSavePath.TrimEnd('/');

            using var client = Connect(conn);
            logger.Information("Fetching remote save from {user}@{host}:{path}", conn.Username, conn.Host, remote);

            foreach (var name in TopLevelFiles)
            {
                var remotePath = $"{remote}/{name}";
                if (client.Exists(remotePath))
                    DownloadTo(client, remotePath, Path.Join(localDir, name));
            }

            var playersRemote = $"{remote}/Players";
            if (client.Exists(playersRemote))
            {
                foreach (var entry in client.ListDirectory(playersRemote))
                {
                    if (entry.IsDirectory || !entry.Name.EndsWith(".sav", StringComparison.OrdinalIgnoreCase))
                        continue;
                    DownloadTo(client, entry.FullName, Path.Join(localDir, "Players", entry.Name));
                }
            }

            client.Disconnect();
            return localDir;
        }

        private static SftpClient Connect(RemoteSaveConnection conn)
        {
            AuthenticationMethod auth = !string.IsNullOrEmpty(conn.PrivateKeyPath)
                ? new PrivateKeyAuthenticationMethod(conn.Username, new PrivateKeyFile(conn.PrivateKeyPath))
                : new PasswordAuthenticationMethod(conn.Username, conn.Password ?? "");

            var info = new ConnectionInfo(conn.Host, conn.Port, conn.Username, auth)
            {
                // Fail fast so an unreachable server doesn't hang save detection.
                Timeout = TimeSpan.FromSeconds(8),
            };

            var client = new SftpClient(info);
            client.Connect();
            return client;
        }

        // Download to a temp file then atomically move into place, so a failed/partial download
        // never corrupts the cached save. The move also trips StandardSaveGame's FileSystemWatcher,
        // which drives the in-app "save changed — reload?" flow on re-fetch.
        private static void DownloadTo(SftpClient client, string remotePath, string localPath)
        {
            var tmp = localPath + ".tmp";
            using (var fs = File.Create(tmp))
                client.DownloadFile(remotePath, fs);
            File.Move(tmp, localPath, overwrite: true);
        }
    }
}
