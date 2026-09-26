using GenericMCPServerBackend.Core.Model;
using System.Collections.Generic;
using System.Threading;

namespace GenericMCPServerBackend.Core.Services
{
    /// <summary>
    /// The business-logic of this codeunit. It contains no tool, no resource and no prompt of its own: it makes the
    /// mcp-servers available which an administrator declared, and it runs the tool, reads the resource respectively
    /// builds the prompt which belongs to one of them.
    /// </summary>
    public interface IBusinessLogicService
    {
        /// <summary>
        /// Returns the mcp-servers which are declared in the tool-definitions-file.
        /// </summary>
        /// <returns>The declared mcp-servers; empty if no tool-definitions-file was configured.</returns>
        IReadOnlyList<MCPServerDefinition> GetMCPServers();

        /// <summary>
        /// Returns the mcp-server which is reachable under the given route.
        /// </summary>
        /// <param name="route">The path of a request.</param>
        /// <returns>The mcp-server, or null if the route belongs to none of them.</returns>
        MCPServerDefinition GetMCPServerOfRoute(string route);

        /// <summary>
        /// Runs the tool of the given mcp-server with the given arguments.
        /// </summary>
        /// <param name="urlNamespace">The namespace of the mcp-server the tool belongs to.</param>
        /// <param name="toolName">The name of the tool, which has to be one of the tools of that mcp-server.</param>
        /// <param name="arguments">The values of the parameters of the tool, keyed by the name of the parameter.</param>
        /// <param name="cancellationToken">Stops the tool while it runs, which is how a client that cancelled its call reaches the running script respectively the running web-request.</param>
        /// <returns>What the tool answered.</returns>
        string RunTool(string urlNamespace, string toolName, IReadOnlyDictionary<string, string> arguments, CancellationToken cancellationToken);

        /// <summary>
        /// Reads the resource of the given mcp-server which is declared under the given uri.
        ///
        /// A resource takes no argument: its uri is the whole request, so what it answers is decided by the
        /// declaration alone.
        /// </summary>
        /// <param name="urlNamespace">The namespace of the mcp-server the resource belongs to.</param>
        /// <param name="uri">The uri of the resource, which has to be one of the resources of that mcp-server.</param>
        /// <returns>The content of the resource.</returns>
        string ReadResource(string urlNamespace, string uri);

        /// <summary>
        /// Builds the prompt of the given mcp-server with the given arguments.
        ///
        /// A prompt is run like a tool and differs in what a client does with the answer: it hands it to its model
        /// instead of reading it as the result of something which was done.
        /// </summary>
        /// <param name="urlNamespace">The namespace of the mcp-server the prompt belongs to.</param>
        /// <param name="promptName">The name of the prompt, which has to be one of the prompts of that mcp-server.</param>
        /// <param name="arguments">The values of the arguments of the prompt, keyed by the name of the argument.</param>
        /// <returns>The text of the prompt.</returns>
        string GetPrompt(string urlNamespace, string promptName, IReadOnlyDictionary<string, string> arguments);

        /// <summary>
        /// Returns the values which the given argument of the given prompt of the given mcp-server can have.
        ///
        /// The completion which is declared for that argument is run like a tool and is told which argument it is
        /// asked about and which value was entered so far; every non-empty line it writes is one value.
        /// </summary>
        /// <param name="urlNamespace">The namespace of the mcp-server the prompt belongs to.</param>
        /// <param name="promptName">The name of the prompt the argument belongs to.</param>
        /// <param name="argumentName">The name of the argument whose values are asked for.</param>
        /// <param name="argumentValue">The value which was entered so far, which the completion uses to decide which values it offers.</param>
        /// <returns>The possible values; empty when that argument declares no completion or when no prompt of that name is declared.</returns>
        IReadOnlyList<string> GetCompletions(string urlNamespace, string promptName, string argumentName, string argumentValue);

        /// <summary>
        /// Removes the values of the credentials from a text which is about to be handed to a client.
        ///
        /// The message of a failed tool-call goes to the client, and a script which wrote its credential into a
        /// stacktrace would hand it over with it.
        /// </summary>
        /// <param name="text">The text, usually the message of a failure.</param>
        /// <returns>The text in which every known credential-value was replaced.</returns>
        string RemoveCredentialValuesFrom(string text);

        /// <summary>States whether the given token is the token of the administrator of this server.</summary>
        bool RequestIsFromAnAdministrator(string adminToken);

        /// <summary>States which client the given client-administration-token belongs to.</summary>
        bool TryGetClientOfAdministrationToken(string clientAdministrationToken, out string client);

        /// <summary>
        /// Decides whether the given access-token belongs to a client which may use the given mcp-server.
        /// </summary>
        /// <param name="mcpServer">The mcp-server the request asks for.</param>
        /// <param name="accessToken">The token the request carried.</param>
        /// <param name="client">The client the token belongs to, if it belongs to any - regardless of whether that client may reach this mcp-server.</param>
        /// <returns>Whether the client may reach that mcp-server.</returns>
        bool ClientMayReachMCPServer(MCPServerDefinition mcpServer, string accessToken, out string client);

        /// <summary>The clients which exist, together with their roles and the amount of access-tokens they hold.</summary>
        IReadOnlyCollection<ClientOverview> GetClients();

        /// <exception cref="ArgumentException">Thrown when the name is not usable.</exception>
        /// <exception cref="InvalidOperationException">Thrown when a client of that name already exists.</exception>
        /// <returns>The client-administration-token. This is the only moment at which it exists in a readable form.</returns>
        string CreateClient(string client);

        /// <exception cref="InvalidOperationException">Thrown when no client of that name exists.</exception>
        /// <returns>The new client-administration-token. This is the only moment at which it exists in a readable form, and the previous one stops working at once.</returns>
        string RenewClientAdministrationToken(string client);

        /// <summary>
        /// Removes the given client together with its access-tokens and its roles: a client which does not exist
        /// anymore must not keep an access, and a client which is created again later must not inherit what its
        /// predecessor had.
        /// </summary>
        /// <returns>False if no client of that name existed.</returns>
        bool DeleteClient(string client);

        /// <summary>The roles which exist.</summary>
        IReadOnlyCollection<string> GetRoles();

        /// <exception cref="ArgumentException">Thrown when the given name is not usable as the name of a role.</exception>
        /// <returns>False if a role of that name already exists.</returns>
        bool CreateRole(string role);

        /// <summary>Removes the given role and every assignment of it.</summary>
        /// <returns>False if no role of that name existed.</returns>
        bool DeleteRole(string role);

        /// <summary>The roles of the given client.</summary>
        /// <returns>The roles of that client, or null if no client of that name exists.</returns>
        IReadOnlyCollection<string> GetRolesOfClient(string client);

        /// <summary>Puts the given client into the given role.</summary>
        /// <exception cref="ArgumentException">Thrown when the name of the role is not usable.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the client or the role does not exist.</exception>
        /// <returns>False if the client is in that role already.</returns>
        bool AssignRole(string client, string role);

        /// <summary>Removes the given client from the given role.</summary>
        /// <returns>False if the client was not in that role.</returns>
        bool RemoveRoleOfClient(string client, string role);

        /// <summary>The access-tokens of the given client, together with whether each of them is still valid.</summary>
        IReadOnlyList<AccessTokenOverview> GetAccessTokensOf(string client);

        /// <exception cref="ArgumentException">Thrown when the name of the client is not usable or when the token would be valid for less than one day.</exception>
        /// <returns>The access-token. This is the only moment at which it exists in a readable form.</returns>
        string CreateAccessToken(string client, int validForDays);

        /// <summary>Removes the access-token of the given client which has the given hash.</summary>
        /// <returns>False if that client has no token with that hash.</returns>
        bool RemoveAccessToken(string client, string hash);
    }
}
