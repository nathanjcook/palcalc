using System;
using System.IO;
using System.Runtime.Serialization;
using System.Security.Cryptography;
using System.Text;

namespace PalCalc.SaveReader
{
    /// <summary>
    /// Connection details for a remote (dedicated-server) Palworld save fetched over SFTP.
    /// The save is mirrored into a local cache dir so the existing <see cref="StandardSaveGame"/>
    /// parser can read it unchanged.
    /// </summary>
    public class RemoteSaveConnection
    {
        public string Host { get; set; }
        public int Port { get; set; } = 22;
        public string Username { get; set; } = "root";

        // Key auth is preferred (many hardened dedicated servers disable password auth for root).
        public string PrivateKeyPath { get; set; }

        // Optional password auth (used only when PrivateKeyPath is empty). Deliberately NOT
        // persisted - it's kept for the current session only, so it never lands on disk. A
        // password-auth save is restored from its local cache on startup and re-prompts when
        // refreshed. Key auth is still the recommended path.
        [IgnoreDataMember]
        public string Password { get; set; }

        // Remote directory containing Level.sav (e.g. .../Pal/Saved/SaveGames/0/<world-id>)
        public string RemoteSavePath { get; set; }

        // Stable identity for dedup / lookup. Computed - not persisted.
        [IgnoreDataMember]
        public string Id => $"{Username}@{Host}:{Port}{RemoteSavePath}";

        // Deterministic local mirror directory for this connection. Computed - not persisted.
        [IgnoreDataMember]
        public string LocalCacheDir
        {
            get
            {
                var hash = Convert.ToHexString(SHA1.HashData(Encoding.UTF8.GetBytes(Id)))[..16];
                return Path.Join(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "PalCalc", "RemoteSaves", hash
                );
            }
        }

        /// <summary>
        /// Parse a connection string of the form "user@host[:port]:/remote/save/path".
        /// `user` defaults to "root" and `port` to 22 when omitted.
        /// </summary>
        public static RemoteSaveConnection Parse(string connectionString, string privateKeyPath)
        {
            var s = (connectionString ?? "").Trim();

            var user = "root";
            var at = s.IndexOf('@');
            if (at >= 0)
            {
                user = s[..at];
                s = s[(at + 1)..];
            }

            // The remote path begins at the first ":/" — everything before is host[:port].
            var sep = s.IndexOf(":/", StringComparison.Ordinal);
            if (sep < 0)
                throw new FormatException("Expected format: user@host[:port]:/remote/save/path");

            var hostPort = s[..sep];
            var path = s[(sep + 1)..]; // keep the leading '/'

            var host = hostPort;
            var port = 22;
            var colon = hostPort.IndexOf(':');
            if (colon >= 0)
            {
                host = hostPort[..colon];
                if (!int.TryParse(hostPort[(colon + 1)..], out port) || port <= 0)
                    port = 22;
            }

            return new RemoteSaveConnection
            {
                Host = host,
                Port = port,
                Username = user,
                PrivateKeyPath = privateKeyPath,
                RemoteSavePath = path,
            };
        }
    }
}
