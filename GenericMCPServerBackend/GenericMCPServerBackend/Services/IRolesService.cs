using System.Collections.Generic;

namespace GenericMCPServerBackend.Core.Services
{
    /// <summary>
    /// The roles which exist and which client is in which of them.
    ///
    /// A role is what connects a client to an mcp-server: a declaration states which roles may use an mcp-server, and
    /// the roles here state which client is in which role. That way a new client needs no change of the declaration,
    /// and a client which loses its access needs no new declaration either.
    /// </summary>
    public interface IRolesService
    {
        /// <summary>The roles which exist.</summary>
        IReadOnlyCollection<string> GetRoles();

        /// <summary>
        /// Creates the given role.
        /// </summary>
        /// <returns>False if a role of that name already exists.</returns>
        bool CreateRole(string role);

        /// <summary>
        /// Removes the given role and every assignment of it.
        /// </summary>
        /// <returns>False if no role of that name existed.</returns>
        bool DeleteRole(string role);

        /// <summary>
        /// Returns the roles of the given client.
        /// </summary>
        /// <returns>Its roles; empty when the client is in none, which is the case for every client which nobody put into one.</returns>
        IReadOnlyCollection<string> GetRolesOf(string client);

        /// <summary>
        /// Puts the given client into the given role.
        /// </summary>
        /// <returns>False if the client is in that role already.</returns>
        bool AssignRole(string client, string role);

        /// <summary>
        /// Removes the given client from the given role.
        /// </summary>
        /// <returns>False if the client was not in that role.</returns>
        bool RemoveRoleOfClient(string client, string role);

        /// <summary>Removes every role-assignment of the given client.</summary>
        void RemoveAllRolesOf(string client);

        /// <summary>The file which holds the roles.</summary>
        string GetRolesFile();

        /// <summary>The file which holds which client is in which role.</summary>
        string GetAssignmentsFile();
    }
}
