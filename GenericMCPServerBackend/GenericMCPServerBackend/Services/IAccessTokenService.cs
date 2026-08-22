using GenericMCPServerBackend.Core.Model;
using System.Collections.Generic;

namespace GenericMCPServerBackend.Core.Services
{
    /// <summary>
    /// The access-tokens with which clients reach the mcp-servers of this server.
    /// </summary>
    public interface IAccessTokenService
    {
        /// <summary>
        /// States whether the given token belongs to a client and is still valid.
        /// </summary>
        /// <param name="token">The token which a client sent.</param>
        /// <param name="client">The client the token belongs to, if it is a valid one.</param>
        bool TryGetClient(string token, out string client);

        /// <summary>
        /// Creates a token for the given client, stores it and returns it.
        /// </summary>
        /// <param name="client">The client the token belongs to.</param>
        /// <param name="validForDays">The amount of days the token is valid.</param>
        /// <returns>The token. It is the only moment at which it exists in a readable form, because only its hash is stored.</returns>
        string CreateAccessToken(string client, int validForDays);

        /// <summary>The tokens of the given client. They contain the hash and never the token itself.</summary>
        IReadOnlyList<AccessToken> GetAccessTokensOf(string client);

        /// <summary>
        /// Removes the token of the given client which has the given hash.
        /// </summary>
        /// <returns>False if that client has no token with that hash.</returns>
        bool RemoveAccessToken(string client, string hash);

        /// <summary>Removes every token of the given client.</summary>
        void RemoveAccessTokensOf(string client);

        /// <summary>The file which holds the tokens.</summary>
        string GetFile();
    }
}
