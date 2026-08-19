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
        /// Runs the script of the given tool with the given arguments.
        /// </summary>
        /// <param name="toolName">The name of the tool, which has to be one of the declared ones.</param>
        /// <param name="arguments">The values of the parameters of the tool, keyed by the name of the parameter.</param>
        /// <returns>What the script wrote to stdout.</returns>
        string RunTool(string toolName, IReadOnlyDictionary<string, string> arguments);
    }
}
