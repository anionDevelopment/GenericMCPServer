using System.Collections.Generic;

namespace GenericMCPServerBackend.Core.Services
{
    /// <summary>
    /// The clients which exist and the tokens with which they administrate themselves.
    /// </summary>
    public interface IClientService
    {
        /// <summary>The clients which exist.</summary>
        IReadOnlyCollection<string> GetClients();

        bool ClientExists(string client);

        /// <summary>
        /// Creates the given client and returns the token with which it administrates its own access-tokens.
        ///
        /// Whether a client of that name already exists is decided by the business-logic-service before this is called,
        /// so an implementation stores what it is asked to store instead of checking that rule a second time.
        /// </summary>
        /// <returns>The client-administration-token. This is the only moment at which it exists in a readable form, because only its hash is stored.</returns>
        string CreateClient(string client);

        /// <summary>
        /// Creates a new client-administration-token for the given client and returns it. The previous one stops
        /// working at once.
        ///
        /// Whether that client exists is decided by the business-logic-service before this is called, so an
        /// implementation replaces the row it is asked to replace instead of checking that rule a second time.
        /// </summary>
        /// <returns>The new token. This is the only moment at which it exists in a readable form, because only its hash is stored.</returns>
        string RenewAdministrationToken(string client);

        /// <summary>
        /// Removes the given client.
        /// </summary>
        /// <returns>False if no client of that name existed.</returns>
        bool DeleteClient(string client);

        /// <summary>
        /// States which client the given client-administration-token belongs to.
        /// </summary>
        bool TryGetClientOfAdministrationToken(string token, out string client);

        /// <summary>The file which holds the clients.</summary>
        string GetFile();
    }
}
