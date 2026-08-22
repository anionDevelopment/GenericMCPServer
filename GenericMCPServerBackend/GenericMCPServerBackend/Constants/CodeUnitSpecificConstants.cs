namespace GenericMCPServerBackend.Core.Constants
{
    /// <summary>
    /// Contains constants specific to the business-logic of this codeunit.
    /// </summary>
    public class CodeUnitSpecificConstants
    {
        /// <summary>
        /// The route of the administration of the server: clients, roles and their assignments. It is stated here and
        /// not only at the controller, because the middleware which decides who may reach it needs the same value.
        /// </summary>
        internal const string AdminRoute = GRYLibrary.Core.APIServer.Settings.Configuration.ServerConfiguration.APIRoutePrefix + "/v1/Admin";

        /// <summary>
        /// The route with which a client administrates its own access-tokens.
        /// </summary>
        internal const string ClientAdministrationRoute = GRYLibrary.Core.APIServer.Settings.Configuration.ServerConfiguration.APIRoutePrefix + "/v1/ClientAdministration";

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
