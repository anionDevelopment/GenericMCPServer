namespace GenericMCPServerBackend.Core.Constants
{
    /// <summary>
    /// Contains constants specific to the business-logic of this codeunit.
    /// </summary>
    public class CodeUnitSpecificConstants
    {
        /// <summary>
        /// The route under which the MCP-endpoint of this server is available. A MCP-client is configured with exactly
        /// this address; all messages of the protocol are exchanged over it.
        /// </summary>
        internal const string MCPRoute = "/API/v1/MCP";
    }
}
