using GenericMCPServerBackend.Core.Model;
using System.Collections.Generic;

namespace GenericMCPServerBackend.Core.Services
{
    /// <summary>
    /// The business-logic of this codeunit. It contains no tool of its own: it makes the tools available which an
    /// administrator declared, and it runs the script which belongs to one of them.
    /// </summary>
    public interface IBusinessLogicService
    {
        /// <summary>
        /// Returns the tools which are declared in the tool-definitions-file.
        /// </summary>
        /// <returns>The declared tools; empty if no tool-definitions-file was configured.</returns>
        IReadOnlyList<ToolDefinition> GetToolDefinitions();

        /// <summary>
        /// States whether the given tool can be used without an access-token.
        /// </summary>
        /// <param name="toolName">The name of the tool.</param>
        /// <returns>True only if a tool of that name is declared and is declared as anonymous.</returns>
        bool ToolIsAnonymous(string toolName);

        /// <summary>
        /// Runs the script of the given tool with the given arguments.
        /// </summary>
        /// <param name="toolName">The name of the tool, which has to be one of the declared ones.</param>
        /// <param name="arguments">The values of the parameters of the tool, keyed by the name of the parameter.</param>
        /// <returns>What the script wrote to stdout.</returns>
        string RunTool(string toolName, IReadOnlyDictionary<string, string> arguments);

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
