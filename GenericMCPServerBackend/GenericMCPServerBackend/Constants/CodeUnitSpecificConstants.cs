namespace GenericMCPServerBackend.Core.Constants
{
    /// <summary>
    /// Contains constants specific to the business-logic of this codeunit.
    /// </summary>
    public class CodeUnitSpecificConstants
    {
        /// <summary>
        /// The end of the route of an mcp-server. The whole route consists of the namespace which the server declares
        /// and this end, so one process can host more than one mcp-server.
        /// </summary>
        internal const string MCPRouteSuffix = "/message";

        /// <summary>
        /// The route under which the mcp-server of the given namespace is available. A MCP-client is configured with
        /// exactly this address; all messages of the protocol are exchanged over it.
        /// </summary>
        internal static string GetMCPRoute(string urlNamespace)
        {
            return $"/{urlNamespace}{MCPRouteSuffix}";
        }
    }
}
