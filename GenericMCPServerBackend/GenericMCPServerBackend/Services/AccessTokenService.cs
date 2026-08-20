using GenericMCPServerBackend.Core.Model;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace GenericMCPServerBackend.Core.Services
{
    /// <summary>
    /// Keeps the access-tokens in a csv-file in the configuration-folder.
    ///
    /// A file is enough for this: the amount of tokens of a server is small, an administrator can read who has access
    /// without a tool, and a backup of the configuration-folder contains them. A database would only add a component
    /// which has to run, be reachable and be migrated.
    ///
    /// Only the hash of a token is stored. The token itself exists exactly once, namely in the moment it is created:
    /// whoever gets the file into their hands therefore holds no access, and a token which somebody lost can not be
    /// looked up but only replaced.
    /// </summary>
    public sealed class AccessTokenService : IAccessTokenService
    {
        /// <summary>The name of the file in the configuration-folder.</summary>
        public const string FileName = "AccessTokens.csv";

        private const char Separator = ';';

        private const string Header = "client;tokenhash;validuntil";

        /// <summary>
        /// The beginning of a token. It makes a token recognizable as one - in a log, in a configuration-file of a
        /// client or in a scan which searches for secrets which were committed by accident.
        /// </summary>
        private const string TokenPrefix = "gmcp_";

        /// <summary>
        /// The form of the name of a client. It may not contain the separator of the file, and it is written into a
        /// log, so it stays at characters which mean the same everywhere.
        /// </summary>
        private static readonly Regex ClientRegex = new Regex("^[A-Za-z0-9_-]{1,128}$", RegexOptions.Compiled);

        private readonly string _File;

        /// <param name="file">The file which holds the tokens. It is created when it does not exist yet.</param>
        public AccessTokenService(string file)
        {
            if (string.IsNullOrWhiteSpace(file))
            {
                throw new ArgumentException("The file for the access-tokens must be set.", nameof(file));
            }
            this._File = Path.GetFullPath(file);
            this.EnsureFileExists();
        }

        /// <summary>The default location of the file: it belongs to the configuration of the server.</summary>
        public static string GetDefaultFile(string configurationFolder)
        {
            return Path.Combine(configurationFolder, FileName);
        }

        public string GetFile()
        {
            return this._File;
        }

        /// <summary>
        /// Creates the file with its header when it does not exist yet, so that an administrator finds a file which
        /// shows what belongs into it instead of having to know its format.
        /// </summary>
        private void EnsureFileExists()
        {
            if (File.Exists(this._File))
            {
                return;
            }
            string folder = Path.GetDirectoryName(this._File);
            if (!string.IsNullOrEmpty(folder))
            {
                Directory.CreateDirectory(folder);
            }
            File.WriteAllLines(this._File, new string[] { Header }, Encoding.UTF8);
        }

        public bool TryGetClient(string token, out string client)
        {
            client = null;
            if (string.IsNullOrWhiteSpace(token))
            {
                return false;
            }
            string hash = GetHash(token);
            DateTime now = DateTime.UtcNow;
            // The file is read for every check instead of being held in memory: a token which was just created works
            // immediately, without a restart of the server, and the file stays the only place which decides.
            foreach (AccessToken accessToken in this.ReadAll())
            {
                // A token says which client is calling. Whether that client may use the mcp-server it asks for is
                // decided by the roles of the client and not by the token.
                if (string.Equals(accessToken.Hash, hash, StringComparison.OrdinalIgnoreCase) && accessToken.IsValidAt(now))
                {
                    client = accessToken.Client;
                    return true;
                }
            }
            return false;
        }

        public string CreateAccessToken(string client, int validForDays)
        {
            if (client == null || !ClientRegex.IsMatch(client))
            {
                throw new ArgumentException($"\"{client}\" is not a usable name of a client. Such a name may consist of letters, digits, underscores and hyphens.", nameof(client));
            }
            if (validForDays < 1)
            {
                throw new ArgumentException($"A token has to be valid for at least one day, but {validForDays} day(s) were given.", nameof(validForDays));
            }
            string token = TokenPrefix + Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
            DateTime validUntil = DateTime.UtcNow.AddDays(validForDays);
            this.EnsureFileExists();
            File.AppendAllLines(this._File, new string[] { $"{client}{Separator}{GetHash(token)}{Separator}{FormatPointInTime(validUntil)}" }, Encoding.UTF8);
            return token;
        }

        /// <summary>
        /// Reads the tokens.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when a line of the file is not a token. A line which can not be read is not skipped, because skipping it would silently take an access away or leave one in place.</exception>
        public IReadOnlyList<AccessToken> ReadAll()
        {
            List<AccessToken> result = new List<AccessToken>();
            if (!File.Exists(this._File))
            {
                return result;
            }
            string[] lines = File.ReadAllLines(this._File);
            for (int index = 0; index < lines.Length; index++)
            {
                string line = lines[index];
                if (string.IsNullOrWhiteSpace(line) || string.Equals(line.Trim(), Header, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                string[] parts = line.Split(Separator);
                if (parts.Length != 3)
                {
                    throw new InvalidOperationException($"Line {index + 1} of \"{this._File}\" does not consist of the three values \"{Header}\".");
                }
                if (!DateTime.TryParse(parts[2].Trim(), CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out DateTime validUntil))
                {
                    throw new InvalidOperationException($"Line {index + 1} of \"{this._File}\" states \"{parts[2].Trim()}\" as the point in time up to which the token is valid, which is not a readable point in time.");
                }
                result.Add(new AccessToken(parts[0].Trim(), parts[1].Trim(), validUntil));
            }
            return result;
        }

        private static string FormatPointInTime(DateTime pointInTimeInUTC)
        {
            return pointInTimeInUTC.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);
        }

        private static string GetHash(string token)
        {
            return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))).ToLowerInvariant();
        }
    }
}
