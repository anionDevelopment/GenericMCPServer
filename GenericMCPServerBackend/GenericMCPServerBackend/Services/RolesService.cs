using GenericMCPServerBackend.Core.Misc;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace GenericMCPServerBackend.Core.Services
{
    /// <summary>
    /// Keeps the roles and their assignments in two csv-files in the configuration-folder.
    ///
    /// The two are separated because they answer two questions: which roles this server knows at all (which is what a
    /// declaration of an mcp-server refers to), and which client is in which of them. A role therefore exists before
    /// anybody is in it, which is what makes it possible to declare an mcp-server for a role whose clients are created
    /// later.
    ///
    /// A client can be in no role, in one or in several. A client which is in no role reaches no mcp-server which is
    /// not anonymous, which is the safe direction for a client which somebody forgot to put into a role.
    /// </summary>
    public sealed class RolesService : IRolesService
    {
        /// <summary>The name of the file which holds the roles.</summary>
        public const string FileNameOfTheRoles = "Roles.csv";

        /// <summary>The name of the file which holds which client is in which role.</summary>
        public const string FileNameOfTheAssignments = "RoleAssignments.csv";

        private const string HeaderOfTheRoles = "role";

        private const string HeaderOfTheAssignments = "client;role";

        /// <summary>
        /// The form of the name of a role. It may not contain the separator of the file, and it is stated in the
        /// declaration of an mcp-server, so it stays at characters which mean the same everywhere.
        /// </summary>
        private static readonly Regex RoleRegex = new Regex("^[A-Za-z0-9_-]{1,128}$", RegexOptions.Compiled);

        private readonly CsvFile _Roles;

        private readonly CsvFile _Assignments;

        public RolesService(string rolesFile, string assignmentsFile)
        {
            this._Roles = new CsvFile(rolesFile, HeaderOfTheRoles);
            this._Assignments = new CsvFile(assignmentsFile, HeaderOfTheAssignments);
        }

        /// <summary>The default location of the file which holds the roles.</summary>
        public static string GetDefaultRolesFile(string configurationFolder)
        {
            return Path.Combine(configurationFolder, FileNameOfTheRoles);
        }

        /// <summary>The default location of the file which holds the assignments: next to the roles themselves.</summary>
        public static string GetDefaultAssignmentsFile(string configurationFolder)
        {
            return Path.Combine(configurationFolder, FileNameOfTheAssignments);
        }

        public string GetRolesFile()
        {
            return this._Roles.GetFile();
        }

        public string GetAssignmentsFile()
        {
            return this._Assignments.GetFile();
        }

        /// <exception cref="ArgumentException">Thrown when the given name is not usable as the name of a role.</exception>
        public static void AssertIsUsableRoleName(string role)
        {
            if (role == null || !RoleRegex.IsMatch(role))
            {
                throw new ArgumentException($"\"{role}\" is not a usable name of a role. Such a name may consist of letters, digits, underscores and hyphens.", nameof(role));
            }
        }

        public IReadOnlyCollection<string> GetRoles()
        {
            return this._Roles.ReadRows().Select(row => row[0]).ToList();
        }

        public bool CreateRole(string role)
        {
            AssertIsUsableRoleName(role);
            if (this.GetRoles().Any(existing => string.Equals(existing, role, StringComparison.OrdinalIgnoreCase)))
            {
                return false;
            }
            this._Roles.AppendRow(role);
            return true;
        }

        public bool DeleteRole(string role)
        {
            bool removed = this._Roles.RemoveRows(row => string.Equals(row[0], role, StringComparison.OrdinalIgnoreCase));
            if (removed)
            {
                // The assignments of a role which does not exist anymore are removed as well: otherwise a role which is
                // created again later would silently bring its former members back.
                this._Assignments.RemoveRows(row => string.Equals(row[1], role, StringComparison.OrdinalIgnoreCase));
            }
            return removed;
        }

        public IReadOnlyCollection<string> GetRolesOf(string client)
        {
            HashSet<string> result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(client))
            {
                return result;
            }
            foreach (string[] row in this._Assignments.ReadRows())
            {
                if (string.Equals(row[0], client, StringComparison.OrdinalIgnoreCase))
                {
                    result.Add(row[1]);
                }
            }
            return result;
        }

        /// <exception cref="InvalidOperationException">Thrown when the role does not exist. A client is not put into a role which nobody created, because that would look like an access which no declaration can ever grant.</exception>
        public bool AssignRole(string client, string role)
        {
            ClientService.AssertIsUsableClientName(client);
            AssertIsUsableRoleName(role);
            if (!this.GetRoles().Any(existing => string.Equals(existing, role, StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException($"The role \"{role}\" does not exist.");
            }
            if (this.GetRolesOf(client).Contains(role, StringComparer.OrdinalIgnoreCase))
            {
                return false;
            }
            this._Assignments.AppendRow(client, role);
            return true;
        }

        public bool RemoveRoleOfClient(string client, string role)
        {
            return this._Assignments.RemoveRows(row => string.Equals(row[0], client, StringComparison.OrdinalIgnoreCase) && string.Equals(row[1], role, StringComparison.OrdinalIgnoreCase));
        }

        public void RemoveAllRolesOf(string client)
        {
            this._Assignments.RemoveRows(row => string.Equals(row[0], client, StringComparison.OrdinalIgnoreCase));
        }
    }
}
