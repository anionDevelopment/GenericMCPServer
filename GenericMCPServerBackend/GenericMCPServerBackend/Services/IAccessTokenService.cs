namespace GenericMCPServerBackend.Core.Services
{
    /// <summary>
    /// The access-tokens with which clients reach this server.
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

        /// <summary>The file which holds the tokens.</summary>
        string GetFile();
    }
}
