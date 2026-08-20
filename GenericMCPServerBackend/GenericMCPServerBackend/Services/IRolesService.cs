using System.Collections.Generic;

namespace GenericMCPServerBackend.Core.Services
{
    /// <summary>
    /// The roles the clients are in.
    /// </summary>
    public interface IRolesService
    {
        /// <summary>
        /// Returns the roles of the given client.
        /// </summary>
        /// <param name="client">The client.</param>
        /// <returns>Its roles; empty when the client is in none, which is the case for every client which nobody put into one.</returns>
        IReadOnlyCollection<string> GetRolesOf(string client);

        /// <summary>The file which holds the roles.</summary>
        string GetFile();
    }
}
