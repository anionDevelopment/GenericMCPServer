using GenericMCPServerBackend.Core.Misc;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace GenericMCPServerBackend.Core.Services
{
    /// <summary>
    /// Keeps the clients in a csv-file in the configuration-folder.
    ///
    /// A client is created by an administrator and gets one token with which it administrates its own access-tokens.
    /// That separates the two questions which are usually answered by two different people: an administrator decides
    /// which clients exist and which roles they have, and whoever runs a client decides how many access-tokens it uses
    /// and when they are exchanged.
    /// </summary>
    public sealed class ClientService : IClientService
    {
        /// <summary>The name of the file in the configuration-folder.</summary>
        public const string FileName = "ClientAdministrationTokens.csv";

        private const string Header = "client;administrationtokenhash";

        /// <summary>
        /// The form of the name of a client. It may not contain the separator of the file, and it is written into a
        /// log, so it stays at characters which mean the same everywhere.
        /// </summary>
        private static readonly Regex ClientRegex = new Regex("^[A-Za-z0-9_-]{1,128}$", RegexOptions.Compiled);

        private readonly CsvFile _File;

        public ClientService(string file)
        {
            this._File = new CsvFile(file, Header);
        }

        /// <summary>The default location of the file: it belongs to the configuration of the server.</summary>
        public static string GetDefaultFile(string configurationFolder)
        {
            return Path.Combine(configurationFolder, FileName);
        }

        public string GetFile()
        {
            return this._File.GetFile();
        }

        /// <summary>
        /// Checks that the given name is usable as the name of a client.
        /// </summary>
        /// <exception cref="ArgumentException">Thrown when it is not.</exception>
        public static void AssertIsUsableClientName(string client)
        {
            if (client == null || !ClientRegex.IsMatch(client))
            {
                throw new ArgumentException($"\"{client}\" is not a usable name of a client. Such a name may consist of letters, digits, underscores and hyphens.", nameof(client));
            }
        }

        public IReadOnlyCollection<string> GetClients()
        {
            return this._File.ReadRows().Select(row => row[0]).ToList();
        }

        public bool ClientExists(string client)
        {
            return this.GetClients().Any(existing => string.Equals(existing, client, StringComparison.OrdinalIgnoreCase));
        }

        /// <remarks>
        /// Whether the name is still free is decided by the business-logic-service and not here, so this adds the row it
        /// is asked to add. A check at this place as well would be a second one for the same rule.
        /// </remarks>
        /// <exception cref="ArgumentException">Thrown when the name is not usable.</exception>
        public string CreateClient(string client)
        {
            AssertIsUsableClientName(client);
            string token = Token.Create(Token.PrefixOfAClientAdministrationToken);
            this._File.AppendRow(client, Token.GetHash(token));
            return token;
        }

        /// <remarks>
        /// Whether the client exists is decided by the business-logic-service and not here, for the same reason it
        /// decides whether a name is still free when a client is created.
        /// </remarks>
        public string RenewAdministrationToken(string client)
        {
            string token = Token.Create(Token.PrefixOfAClientAdministrationToken);
            string hash = Token.GetHash(token);
            // The row is replaced and no second one is added: a client has exactly one token with which it
            // administrates itself, so a renewal has to end the previous one instead of leaving two of them valid.
            this._File.WriteRows(this._File.ReadRows().Select(row => string.Equals(row[0], client, StringComparison.OrdinalIgnoreCase) ? new string[] { row[0], hash } : row));
            return token;
        }

        public bool DeleteClient(string client)
        {
            return this._File.RemoveRows(row => string.Equals(row[0], client, StringComparison.OrdinalIgnoreCase));
        }

        public bool TryGetClientOfAdministrationToken(string token, out string client)
        {
            client = null;
            if (string.IsNullOrWhiteSpace(token))
            {
                return false;
            }
            foreach (string[] row in this._File.ReadRows())
            {
                if (Token.Matches(token, row[1]))
                {
                    client = row[0];
                    return true;
                }
            }
            return false;
        }
    }
}
