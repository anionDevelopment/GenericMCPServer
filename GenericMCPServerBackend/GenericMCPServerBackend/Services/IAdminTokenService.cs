namespace GenericMCPServerBackend.Core.Services
{
    /// <summary>
    /// Answers whether a request carries the token of the administrator of this server.
    /// </summary>
    public interface IAdminTokenService
    {
        /// <summary>The file which holds the hash of the token.</summary>
        string GetFile();

        /// <summary>
        /// States whether the given token is the token of the administrator.
        /// </summary>
        bool Matches(string token);
    }
}
