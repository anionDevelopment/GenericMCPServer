using GenericMCPServerBackend.Core.Misc;
using GenericMCPServerBackend.Core.Model;
using GRYLibrary.Core.APIServer.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace GenericMCPServerBackend.Core.Services
{
    /// <summary>
    /// Keeps the access-tokens in a csv-file in the configuration-folder.
    ///
    /// Only the hash of a token is stored. The token itself exists exactly once, namely in the moment it is created:
    /// whoever gets the file into their hands therefore holds no access, and a token which somebody lost can not be
    /// looked up but only replaced.
    /// </summary>
    public sealed class AccessTokenService : IAccessTokenService
    {
        /// <summary>The name of the file in the configuration-folder.</summary>
        public const string FileName = "AccessTokens.csv";

        private const string Header = "client;tokenhash;validuntil";

        private readonly CsvFile _File;

        /// <summary>
        /// Where the current point in time comes from. It is not asked from the clock directly, so that a testcase can
        /// state which point in time it is and does not have to wait for one.
        /// </summary>
        private readonly ITimeService _TimeService;

        public AccessTokenService(string file, ITimeService timeService)
        {
            this._File = new CsvFile(file, Header);
            this._TimeService = timeService;
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

        public bool TryGetClient(string token, out string client)
        {
            client = null;
            if (string.IsNullOrWhiteSpace(token))
            {
                return false;
            }
            DateTime now = this.GetCurrentPointInTime();
            // A token says which client is calling. Whether that client may use the mcp-server it asks for is decided
            // by the roles of the client and not by the token.
            foreach (AccessToken accessToken in this.ReadAll())
            {
                if (Token.Matches(token, accessToken.Hash) && accessToken.IsValidAt(now))
                {
                    client = accessToken.Client;
                    return true;
                }
            }
            return false;
        }

        /// <exception cref="ArgumentException">Thrown when the name of the client is not usable or when the token would be valid for less than one day.</exception>
        public string CreateAccessToken(string client, int validForDays)
        {
            ClientService.AssertIsUsableClientName(client);
            if (validForDays < 1)
            {
                throw new ArgumentException($"A token has to be valid for at least one day, but {validForDays} day(s) were given.", nameof(validForDays));
            }
            string token = Token.Create(Token.PrefixOfAnAccessToken);
            this._File.AppendRow(client, Token.GetHash(token), FormatPointInTime(this.GetCurrentPointInTime().AddDays(validForDays)));
            return token;
        }

        public IReadOnlyList<AccessToken> GetAccessTokensOf(string client)
        {
            return this.ReadAll().Where(accessToken => string.Equals(accessToken.Client, client, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        public bool RemoveAccessToken(string client, string hash)
        {
            return this._File.RemoveRows(row => string.Equals(row[0], client, StringComparison.OrdinalIgnoreCase) && string.Equals(row[1], hash, StringComparison.OrdinalIgnoreCase));
        }

        public void RemoveAccessTokensOf(string client)
        {
            this._File.RemoveRows(row => string.Equals(row[0], client, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Reads the tokens.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when a line of the file is not a token.</exception>
        public IReadOnlyList<AccessToken> ReadAll()
        {
            List<AccessToken> result = new List<AccessToken>();
            foreach (string[] row in this._File.ReadRows())
            {
                if (!DateTime.TryParse(row[2], CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out DateTime validUntil))
                {
                    throw new InvalidOperationException($"\"{row[2]}\" in \"{this._File.GetFile()}\" is not a readable point in time up to which a token is valid.");
                }
                result.Add(new AccessToken(row[0], row[1], validUntil));
            }
            return result;
        }

        /// <summary>The current point in time in utc.</summary>
        private DateTime GetCurrentPointInTime()
        {
            return this._TimeService.GetCurrentTimeInUTCAsDateTimeOffset().UtcDateTime;
        }

        private static string FormatPointInTime(DateTime pointInTimeInUTC)
        {
            return pointInTimeInUTC.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);
        }
    }
}
