using System;

namespace GenericMCPServerBackend.Tests.TestUtilities
{
    /// <summary>
    /// Puts a declaration of tools into one mcp-server.
    /// </summary>
    /// <remarks>
    /// A tool belongs to an mcp-server, but most testcases are about the tool and not about the mcp-server it belongs
    /// to. They therefore state their tools and let this wrapper put them into one, so that the declaration of the
    /// testcase stays as small as the question it asks.
    /// </remarks>
    public static class MCPServerWrapper
    {
        /// <summary>The namespace of the mcp-server which the testcases use.</summary>
        public const string UrlNamespace = "test";

        /// <param name="toolDefinitions">A declaration of the form <c>{ "tools": [ ... ] }</c>.</param>
        /// <returns>The same tools, declared as the tools of one mcp-server which everybody may use.</returns>
        /// <remarks>
        /// The mcp-server is declared as anonymous: these testcases call the business-logic directly and are not about
        /// who may reach it, so a role would only be a value which every one of them had to carry along.
        /// </remarks>
        public static string WrapInAnMCPServer(string toolDefinitions)
        {
            string tools = toolDefinitions.Trim();
            if (!tools.StartsWith("{", StringComparison.Ordinal) || !tools.EndsWith("}", StringComparison.Ordinal))
            {
                throw new ArgumentException("The declaration has to be a json-object.", nameof(toolDefinitions));
            }
            return "{ \"mcp-servers\": [ { \"url-namespace\": \"" + UrlNamespace + "\", \"anonymous\": true, " + tools.Substring(1, tools.Length - 2).Trim() + " } ] }";
        }
    }
}
