using GRYLibrary.Core.APIServer.Services.Init;
using GRYLibrary.Core.APIServer.Services.Logger;
using GRYLibrary.Core.APIServer.Utilities;
using GRYLibrary.Core.APIServer.Utilities.InitializationStates;
using GenericMCPServerBackend.Core.Configuration;
using System;

namespace GenericMCPServerBackend.Core.Services
{
    /// <summary>
    /// Reports whether this server finished its initialization.
    ///
    /// The state is what the maintenance-route "InitializationState" answers with, so a caller (a container-orchestrator
    /// or a testcase which waits for the server) can tell "not ready yet" from "ready" and from "will never be ready".
    /// Without this service that route fails, because it has nothing to ask.
    /// </summary>
    public class InitializationService : IInitializationService<CommandlineParameter>
    {
        private readonly IBusinessLogicService _BusinessLogicService;
        private readonly IServerLog _Logger;
        private InitializationState _InitializationState = new Uninitialized();

        public InitializationService(IBusinessLogicService businessLogicService, IServerLog logger)
        {
            this._BusinessLogicService = businessLogicService;
            this._Logger = logger;
        }

        public InitializationState GetInitializationState()
        {
            return this._InitializationState;
        }

        public void Initialize(CommandlineParameter commandlineParameter)
        {
            try
            {
                this._InitializationState = new Initializing();
                // There is nothing to prepare here: the declarations are read while the business-logic-service is
                // created, and a declaration which is broken already made the start fail at that point. What is worth
                // stating is which mcp-servers this process hosts and how many tools each of them offers, because that
                // is the first thing to look at when a client sees none.
                foreach (Model.MCPServerDefinition mcpServer in this._BusinessLogicService.GetMCPServers())
                {
                    this._Logger.Logger.Log($"The mcp-server \"{mcpServer.UrlNamespace}\" offers {mcpServer.Tools.Count} tool(s) under \"{Constants.CodeUnitSpecificConstants.GetMCPRoute(mcpServer.UrlNamespace)}\".", Microsoft.Extensions.Logging.LogLevel.Information);
                }
                this._InitializationState = new Initialized();
            }
            catch (Exception exception)
            {
                // Without this the application only reports "initialization failed" while the reason is lost, which
                // makes a broken first start impossible to diagnose.
                this._Logger.Logger.Log("Initialization of the service failed.", exception);
                this._InitializationState = new InitializationFailed();
            }
        }
    }
}
