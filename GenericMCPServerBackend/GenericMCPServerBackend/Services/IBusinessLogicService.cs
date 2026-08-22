using GenericMCPServerBackend.Core.Model;
using System.Collections.Generic;

namespace GenericMCPServerBackend.Core.Services
{
    /// <summary>
    /// The business-logic of this codeunit. It contains no tool of its own: it makes the mcp-servers available which an
    /// administrator declared, and it runs the tool which belongs to one of them.
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
        /// <returns>What the tool answered.</returns>
        string RunTool(string urlNamespace, string toolName, IReadOnlyDictionary<string, string> arguments);

        /// <summary>
        /// Removes the values of the credentials from a text which is about to be handed to a client.
        ///
        /// The message of a failed tool-call goes to the client, and a script which wrote its credential into a
        /// stacktrace would hand it over with it.
        /// </summary>
        /// <param name="text">The text, usually the message of a failure.</param>
        /// <returns>The text in which every known credential-value was replaced.</returns>
        string RemoveCredentialValuesFrom(string text);
    }
}
