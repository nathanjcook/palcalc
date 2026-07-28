using Renci.SshNet;
using Serilog;
using System;
using System.IO;

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

        // Top-level save files to mirror (WorldOption/LocalData may be absent on linux server saves).
        private static readonly string[] TopLevelFiles =
            ["Level.sav", "LevelMeta.sav", "LocalData.sav", "WorldOption.sav"];

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

        /// <summary>
        /// Fetch the save files into <see cref="RemoteSaveConnection.LocalCacheDir"/> and return that path.
        /// Throws on connection/auth failure (callers should catch and surface to the user).
        /// </summary>
        public static string Fetch(RemoteSaveConnection conn)
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

        private static void DownloadTo(SftpClient client, string remotePath, string localPath)
        {
            using var fs = File.Create(localPath);
            client.DownloadFile(remotePath, fs);
        }
    }
}
