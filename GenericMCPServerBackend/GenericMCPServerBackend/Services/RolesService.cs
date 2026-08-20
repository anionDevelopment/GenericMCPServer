using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace GenericMCPServerBackend.Core.Services
{
    /// <summary>
    /// Keeps the roles of the clients in a csv-file next to the access-tokens.
    ///
    /// A role is what connects a client to an mcp-server: a declaration states which roles may use an mcp-server, and
    /// this file states which client is in which role. That way a new client needs no change of the declaration, and a
    /// client which loses its access needs no new declaration either - two things which are done by different people
    /// stay in two different files.
    ///
    /// A client can be in no role, in one or in several. A client which is in no role reaches no mcp-server which is
    /// not anonymous, which is the safe direction for a client which somebody forgot to put into a role.
    /// </summary>
    public sealed class RolesService : IRolesService
    {
        /// <summary>The name of the file in the configuration-folder.</summary>
        public const string FileName = "Roles.csv";

        private const char Separator = ';';

        private const string Header = "client;role";

        private readonly string _File;

        /// <param name="file">The file which holds the roles. It is created when it does not exist yet.</param>
        public RolesService(string file)
        {
            if (string.IsNullOrWhiteSpace(file))
            {
                throw new ArgumentException("The file for the roles must be set.", nameof(file));
            }
            this._File = Path.GetFullPath(file);
            this.EnsureFileExists();
        }

        /// <summary>The default location of the file: next to the access-tokens, because both belong together.</summary>
        public static string GetDefaultFile(string configurationFolder)
        {
            return Path.Combine(configurationFolder, FileName);
        }

        public string GetFile()
        {
            return this._File;
        }

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

        /// <summary>
        /// Returns the roles of the client.
        /// </summary>
        /// <remarks>
        /// The file is read for every request, the same way the access-tokens are: a role which is given or taken away
        /// works immediately and without restarting the server.
        /// </remarks>
        /// <exception cref="InvalidOperationException">Thrown when a line of the file is not a role. A line which can not be read is not skipped, because skipping it would silently take an access away or leave one in place.</exception>
        public IReadOnlyCollection<string> GetRolesOf(string client)
        {
            HashSet<string> result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(client) || !File.Exists(this._File))
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
                if (parts.Length != 2)
                {
                    throw new InvalidOperationException($"Line {index + 1} of \"{this._File}\" does not consist of the two values \"{Header}\".");
                }
                if (string.Equals(parts[0].Trim(), client, StringComparison.OrdinalIgnoreCase))
                {
                    result.Add(parts[1].Trim());
                }
            }
            return result;
        }
    }
}
