namespace GenericMCPServerBackend.Core.Constants
{
    /// <summary>
    /// Contains constants specific to the business-logic of this codeunit.
    /// </summary>
    public class CodeUnitSpecificConstants
    {
        /// <summary>
        /// The route under which the mcp-server of the given namespace is available: the namespace which that server
        /// declares is the whole address, so one process can host more than one mcp-server. A MCP-client is configured
        /// with exactly this address; all messages of the protocol are exchanged over it.
        /// </summary>
        internal static string GetMCPRoute(string urlNamespace)
        {
            return $"/{urlNamespace}";
        }
    }
}
