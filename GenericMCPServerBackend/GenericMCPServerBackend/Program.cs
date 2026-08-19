using GenericMCPServerBackend.Core.Configuration;
using GenericMCPServerBackend.Core.Constants;
using GenericMCPServerBackend.Core.Misc;
using GenericMCPServerBackend.Core.Model;
using GenericMCPServerBackend.Core.Services;
using GRYLibrary.Core.APIServer.CommonRoutes;
using GRYLibrary.Core.APIServer.ConcreteEnvironments;
using GRYLibrary.Core.APIServer.ExecutionModes;
using GRYLibrary.Core.APIServer.MaintenanceRoutes;
using GRYLibrary.Core.APIServer.Mid.Ex;
using GRYLibrary.Core.APIServer.Mid.M05DLog;
using GRYLibrary.Core.APIServer.MidT.Exception;
using GRYLibrary.Core.APIServer.MidT.RLog;
using GRYLibrary.Core.APIServer.Services.Init;
using GRYLibrary.Core.APIServer.Services.Interfaces;
using GRYLibrary.Core.APIServer.Services.Logger;
using GRYLibrary.Core.APIServer.Services.OtherServices;
using GRYLibrary.Core.APIServer.Settings;
using GRYLibrary.Core.APIServer.Settings.Configuration;
using GRYLibrary.Core.APIServer.Utilities;
using GRYLibrary.Core.Logging.GRYLogger;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using ModelContextProtocol.AspNetCore;
using ModelContextProtocol.Server;
using OpenDMSBackend.Core.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace GenericMCPServerBackend.Core
{
    /// <summary>
    /// Entry point of the application, analogous to the backends of the other products. Configures and starts the
    /// API-server with all required services and middleware.
    ///
    /// The MCP-protocol itself is not implemented here: the SDK of Microsoft is registered as a service and its
    /// endpoint is mapped, so this codeunit only contributes the tools (see <see cref="BusinessLogicService"/>).
    /// </summary>
    internal class Program
    {
        internal bool ListenOnEveryIP { get; set; } = true;
        internal bool RunAsync { get; set; } = false;
        internal bool IsRunning { get; set; } = false;
        internal IBusinessLogicService? _BusinessLogicService;
        internal IGRYLog _Log;

        internal IHostApplicationLifetime? _HostApplicationLifetime;
        internal APIServerConfiguration<CodeUnitSpecificConstants, CodeUnitSpecificConfiguration, CommandlineParameter>? _Constants;

        public Program()
        {
            this._Log = new InitialLog().Logger;
        }

        internal static int Main(string[] commandlineArguments)
        {
            return new Program().MainImplementation(commandlineArguments);
        }

        /// <summary>
        /// Configures and starts the API-server with all required services and middleware.
        /// </summary>
        /// <param name="commandlineArguments">The raw command-line arguments passed to the application entry point.</param>
        /// <returns>An integer exit code; <c>0</c> indicates success.</returns>
        internal int MainImplementation(string[] commandlineArguments)
        {
            // The file which declares the tools is read from the commandline-parameters, which are only available
            // once the initialization ran, so it is remembered here and used when the services are registered.
            string toolDefinitionsFile = null;
            this.IsRunning = true;
            int result = Tools.RunAPIServer<CommandlineParameter, CodeUnitSpecificConstants, CodeUnitSpecificConfiguration>(GeneralConstants.CodeUnitName, GeneralConstants.CodeUnitDescription, GRYLibrary.Core.Misc.Version3.Parse(GeneralConstants.CodeUnitVersion), GenericMCPServerBackend.Core.Misc.Utilities.GetEnvironmentTargetType(), GRYLibrary.Core.Misc.Utilities.GetExecutionMode(commandlineArguments), commandlineArguments, null, (apiServerConfiguration) =>
            {
                apiServerConfiguration.SetInitializationInformationAction = (initializationInformation) =>
                {
                    toolDefinitionsFile = initializationInformation.CommandlineParameter.InitialToolDefinitionsFile;
                    string domain = string.IsNullOrWhiteSpace(initializationInformation.CommandlineParameter.InitialDomain) ? Tools.GetDefaultDomainValue(GeneralConstants.CodeUnitName) : initializationInformation.CommandlineParameter.InitialDomain;
                    initializationInformation.InitialApplicationConfiguration.ServerConfiguration.SetDomainAndPublichUrlToDefault(domain);
                    initializationInformation.ApplicationConstants.ListenOnEveryIP = this.ListenOnEveryIP;
                    initializationInformation.ApplicationConstants.ExceptionManagerMiddleware = typeof(DefaultExceptionHandlerMiddleware);
                    initializationInformation.ApplicationConstants.LoggingMiddleware = typeof(DRequestLoggingMiddleware);
                    initializationInformation.ApplicationConstants.CommonRoutesHostInformation = new HostCommonRoutes();
                    initializationInformation.ApplicationConstants.HostMaintenanceInformation = new HostMaintenanceRoutes();
                    initializationInformation.InitialApplicationConfiguration.ApplicationSpecificConfiguration.ConfigurationForExceptionManagerMiddleware = new ExceptionManagerConfiguration();
                    initializationInformation.InitialApplicationConfiguration.ApplicationSpecificConfiguration.CommonRoutesInformation = new CommonRoutesInformation()
                    {
                        ContactLink = $"https://information.{domain}/Products/{GeneralConstants.CodeUnitName}/Contact",
                        LicenseLink = $"https://information.{domain}/Products/{GeneralConstants.CodeUnitName}/License",
                        TermsOfServiceLink = $"https://information.{domain}/Products/{GeneralConstants.CodeUnitName}/TermsOfService"
                    };
                    initializationInformation.InitialApplicationConfiguration.ApplicationSpecificConfiguration.MaintenanceRoutesInformation = new MaintenanceRoutesInformation()
                    {
                        EnableEndpointAvailabilityCheck = initializationInformation.CommandlineParameter.InitialEnableEndpointAvailabilityCheckValue,
                        EnableEndpointInitializationState = initializationInformation.CommandlineParameter.InitialEnableEndpointInitializationStateValue,
                        EnableEndpointCurrentVersion = initializationInformation.CommandlineParameter.InitialEnableEndpointCurrentVersionValue,
                        EnableEndpointShowAllEndpoints = initializationInformation.CommandlineParameter.InitialEnableEndpointShowAllEndpointsValue,
                        EnableEndpointHealthCheck = initializationInformation.CommandlineParameter.InitialEnableEndpointHealthCheckValue,
                        EnableEndpointMetrics = initializationInformation.CommandlineParameter.InitialEnableEndpointMetricsValue,
                    };
                    // Plain HTTP: the MCP-endpoint is meant to be reached over a reverse-proxy or on the local machine,
                    // so this codeunit needs no certificate of its own.
                    initializationInformation.InitialApplicationConfiguration.ServerConfiguration.Protocol = new HTTP();
                    initializationInformation.InitialApplicationConfiguration.ServerConfiguration.Domain = domain;
                    initializationInformation.InitialApplicationConfiguration.ServerConfiguration.HostAPISpecificationForInNonDevelopmentEnvironment = true;
                    initializationInformation.InitialApplicationConfiguration.ApplicationSpecificConfiguration.ConfigurationForDLoggingMiddleware = new DRequestLoggingConfiguration()
                    {
                        NotLoggedRoutes = new HashSet<string>()
                        {
                            @$"^/favicon\.ico$",
                            @$"^/API/Other/Resources/APISpecification/*",
                            @$"^/API/Other/Maintenance/Metrics$",
                            @$"^/API/Other/Maintenance/HealthCheck$",
                        },
                        MaximalLengthofRequestBodies = 500,
                        MaximalLengthOfResponseBodies = 500,
                    };
                };
                apiServerConfiguration.SetFunctionalInformationAction = (functionalInformation) => //technical initialization for every run
                {
                    this._Log = functionalInformation.Logger;//this is now server.log
                    this._Log.Log("Run initialization...");

                    functionalInformation.WebApplicationBuilder.Services.AddSingleton<ITimeService, TimeService>();
                    // The tools are not known at compile-time: they are created here, one per declaration in the
                    // tool-definitions-file. The service is constructed directly (and not by the container), because
                    // the tools have to exist before the container is built and both have to use the same instance.
                    BusinessLogicService businessLogicService = new BusinessLogicService(toolDefinitionsFile);
                    IReadOnlyList<ToolDefinition> toolDefinitions = businessLogicService.GetToolDefinitions();
                    this._Log.Log($"{toolDefinitions.Count} tool(s) declared in \"{toolDefinitionsFile ?? "<no tool-definitions-file configured>"}\".");
                    functionalInformation.WebApplicationBuilder.Services.AddSingleton<IBusinessLogicService>(businessLogicService);

                    // The whole MCP-protocol (the initialize-handshake, "tools/list", "tools/call" and the
                    // error-handling) is implemented by the SDK. This codeunit only hands it the tools.
                    functionalInformation.WebApplicationBuilder.Services.AddMcpServer()
                        // Stateless: every request is answered on its own, so no session-id has to be kept by the
                        // client. That makes the server usable with a plain http-request and is sufficient here,
                        // because the tools hold no state between two calls.
                        .WithHttpTransport(options => options.SessionMode = HttpServerSessionMode.Stateless)
                        .WithTools(toolDefinitions.Select(definition => (McpServerTool)new ScriptTool(businessLogicService, definition)).ToList());

                    functionalInformation.WebApplicationBuilder.Services.AddSingleton<ICommonRoutesInformation>(functionalInformation.PersistedAPIServerConfiguration.ApplicationSpecificConfiguration.CommonRoutesInformation);
                    functionalInformation.WebApplicationBuilder.Services.AddSingleton<IMaintenanceRoutesInformation>(functionalInformation.PersistedAPIServerConfiguration.ApplicationSpecificConfiguration.MaintenanceRoutesInformation);
                    functionalInformation.WebApplicationBuilder.Services.AddSingleton<IRequestLoggingConfiguration>(functionalInformation.PersistedAPIServerConfiguration.ApplicationSpecificConfiguration.ConfigurationForLoggingMiddleware);
                    functionalInformation.WebApplicationBuilder.Services.AddSingleton<IDRequestLoggingConfiguration>(functionalInformation.PersistedAPIServerConfiguration.ApplicationSpecificConfiguration.ConfigurationForDLoggingMiddleware);
                    functionalInformation.WebApplicationBuilder.Services.AddSingleton<IInitializationService<CommandlineParameter>, InitializationService>();
                    functionalInformation.WebApplicationBuilder.Services.AddSingleton<IHealthCheck, HealthCheck>();
                };
                apiServerConfiguration.ConfigureWebApplication = (functionalInformationForWebApplication) =>
                {
                    this._Constants = apiServerConfiguration;
                    this._HostApplicationLifetime = functionalInformationForWebApplication.WebApplication.Services.GetService<IHostApplicationLifetime>();
                    this._BusinessLogicService = functionalInformationForWebApplication.WebApplication.Services.GetService<IBusinessLogicService>();
                    functionalInformationForWebApplication.RunAsync = this.RunAsync;
                    // This is the one endpoint a MCP-client is configured with. It is mapped with its full route,
                    // because it is not a controller and therefore does not get the route-prefix of the API-server.
                    functionalInformationForWebApplication.WebApplication.MapMcp(CodeUnitSpecificConstants.MCPRoute);
                };
            }, this._Log);
            this.IsRunning = false;
            return result;
        }

        /// <summary>
        /// Requests a graceful shutdown of the running API-server and blocks until it has fully stopped.
        /// </summary>
        internal void Stop()
        {
            GRYLibrary.Core.Misc.Utilities.AssertNotNull(this._Constants, nameof(this._Constants)).CancellationTokenSource.Cancel();
            while (this.IsRunning)
            {
                Thread.Sleep(TimeSpan.FromMilliseconds(100));
            }
        }
    }
}
