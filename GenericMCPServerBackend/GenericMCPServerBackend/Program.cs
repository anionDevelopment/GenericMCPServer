using GenericMCPServerBackend.Core.Configuration;
using GenericMCPServerBackend.Core.Constants;
using GenericMCPServerBackend.Core.Misc;
using GenericMCPServerBackend.Core.Model;
using GenericMCPServerBackend.Core.Services;
using GRYLibrary.Core.APIServer.CommonRoutes;
using GRYLibrary.Core.APIServer.ConcreteEnvironments;
using CommandLine;
using GRYLibrary.Core.APIServer.ExecutionModes;
using GRYLibrary.Core.APIServer.ExecutionModes.Visitors;
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
using GRYLibrary.Core.Misc.FilePath;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using ModelContextProtocol.AspNetCore;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

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
        internal CodeUnitSpecificConfiguration? _ApplicationConfiguration;

        /// <summary>
        /// The service which holds the token of the administrator. A testcase reaches the file of that token through
        /// it: the token itself is only written to the log, and the folders of the server are not stated from outside.
        /// </summary>
        internal IAdminTokenService? _AdminTokenService;
        internal IGRYLog _Log;

        internal IHostApplicationLifetime? _HostApplicationLifetime;
        internal APIServerConfiguration<CodeUnitSpecificConstants, CodeUnitSpecificConfiguration, CommandlineParameter>? _Constants;

        /// <summary>
        /// Fired synchronously, still inside <see cref="MainImplementation"/>, once the configuration-folder of this run is known and - for a test-run -
        /// after GRYLibrary has wiped and recreated it, but before the configuration-file (and therefore the default of
        /// <see cref="Configuration.CodeUnitSpecificConfiguration.ToolDefinitionsFile"/>) is read. Firing it any earlier (for example right where the
        /// folder becomes known, during initialization) would have it write into a folder GRYLibrary deletes and recreates for every test-run right
        /// afterwards, which silently discards whatever was written and is exactly the bug this timing avoids. A test which wants the server to find a
        /// declaration without configuring anything explicitly (see IntegrationTestFramework in the test-project) sets this to write it into that folder
        /// in time; production code has no need for it and leaves it null.
        /// </summary>
        internal Action<string>? OnConfigurationFolderResolved { get; set; }

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
            // The folder of the virtual environments of the python-tools is read from the application-constants, which
            // are only available once the initialization ran, so it is remembered here and used when the services are
            // registered.
            string virtualEnvironmentsFolder = null;
            string administrationFolder = null;
            this.IsRunning = true;
            int result = Tools.RunAPIServer<CommandlineParameter, CodeUnitSpecificConstants, CodeUnitSpecificConfiguration>(GeneralConstants.CodeUnitName, GeneralConstants.CodeUnitDescription, GRYLibrary.Core.Misc.Version3.Parse(GeneralConstants.CodeUnitVersion), GenericMCPServerBackend.Core.Misc.Utilities.GetEnvironmentTargetType(), GRYLibrary.Core.Misc.Utilities.GetExecutionMode(commandlineArguments), commandlineArguments, null, (apiServerConfiguration) =>
            {
                apiServerConfiguration.SetInitializationInformationAction = (initializationInformation) =>
                {
                    initializationInformation.InitialApplicationConfiguration.ApplicationSpecificConfiguration.ToolDefinitionsFile = AbstractFilePath.FromString("./Tools.json");
                    virtualEnvironmentsFolder = Path.Combine(initializationInformation.ApplicationConstants.GetDataFolder(), "VirtualEnvironments");
                    administrationFolder = initializationInformation.ApplicationConstants.GetConfigurationFolder();
                    initializationInformation.ApplicationConstants.CustomMiddlewares1.Add(typeof(AccessTokenValidatorMiddleware));
                    initializationInformation.ApplicationConstants.CustomMiddlewares2.Add(typeof(ToolCallFailureStatusCodeMiddleware));
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
                    // For a test-run GRYLibrary wipes and recreates the configuration-folder between the initialization- and the functional-phase (so
                    // every test-run starts clean), so this has to fire here and not during initialization - see the doc-comment of OnConfigurationFolderResolved.
                    this.OnConfigurationFolderResolved?.Invoke(administrationFolder);
                    this._Log = functionalInformation.Logger;//this is now server.log
                    this._Log.Log("Run initialization...");

                    // The time is taken from this service everywhere, so a testcase can state which point in time it
                    // is instead of waiting for one.
                    ITimeService timeService = new TimeService();
                    functionalInformation.WebApplicationBuilder.Services.AddSingleton<ITimeService>(timeService);
                    // The maintenance-health-check-route of GRYLibrary resolves the health-check-service of
                    // ASP.NET, so it has to be registered even though this codeunit contributes no check of its own:
                    // without it that route answers with 500, and it is the route the container-image checks itself
                    // with (see the HEALTHCHECK of the Dockerfile and Other/OnBuildingFinished.py).
                    functionalInformation.WebApplicationBuilder.Services.AddHealthChecks();
                    // The tools are not known at compile-time: they are created here, one per declaration in the
                    // tool-definitions-file. The service is constructed directly (and not by the container), because
                    // the tools have to exist before the container is built and both have to use the same instance.
                    // A tool reaches the http-request it is running in through this accessor, which is what lets a
                    // failed tool-call decide the status-code of the answer.
                    functionalInformation.WebApplicationBuilder.Services.AddHttpContextAccessor();
                    // Only a client with a valid access-token reaches the protocol. The file is created here when it
                    // does not exist yet, so that an administrator finds the place where the tokens belong. These
                    // services are not registered in the container themselves: the business-logic-service is the only
                    // one which decides anything with them (who may reach what, and the administration of clients,
                    // roles and tokens), so it is the only one which needs them.
                    IAccessTokenService accessTokenService = new AccessTokenService(AccessTokenService.GetDefaultFile(administrationFolder), timeService);
                    IClientService clientService = new ClientService(ClientService.GetDefaultFile(administrationFolder));
                    IRolesService rolesService = new RolesService(RolesService.GetDefaultRolesFile(administrationFolder), RolesService.GetDefaultAssignmentsFile(administrationFolder));
                    // The token of the administrator is created here when its file does not exist yet, and it is
                    // written to the log while doing so - that is the one moment it is readable.
                    this._AdminTokenService = new AdminTokenService(AdminTokenService.GetDefaultFile(administrationFolder), this._Log);
                    functionalInformation.WebApplicationBuilder.Services.AddSingleton(functionalInformation.PersistedAPIServerConfiguration.ApplicationSpecificConfiguration);
                    // Resolved against the configuration-folder (the same base every other configurable path of this server, e.g. CertificateFolder, uses),
                    // so a relative value in the configuration-file (like the default "./Tools.json") is not accidentally resolved against the
                    // process' working-directory instead. If the resolved file does not exist (for example because nobody declared any tool yet), the
                    // server still starts and simply offers no tool, exactly like it did before this was configurable at all - it does not abort with
                    // FileNotFoundException, because "no tools declared yet" is an expected, valid state of a freshly set up server.
                    string toolDefinitionsFile = functionalInformation.PersistedAPIServerConfiguration.ApplicationSpecificConfiguration.ToolDefinitionsFile.GetPath(administrationFolder);
                    if (!File.Exists(toolDefinitionsFile))
                    {
                        GRYLibrary.Core.Misc.Utilities.EnsureFileExists(toolDefinitionsFile);
                        File.WriteAllText(toolDefinitionsFile, "{}");
                    }
                    BusinessLogicService businessLogicService = new BusinessLogicService(toolDefinitionsFile, virtualEnvironmentsFolder, clientService, rolesService, accessTokenService, this._AdminTokenService, timeService);
                    IReadOnlyList<MCPServerDefinition> mcpServers = businessLogicService.GetMCPServers();
                    this._Log.Log($"{mcpServers.Count} mcp-server(s) with {mcpServers.Sum(mcpServer => mcpServer.Tools.Count)} tool(s) and {mcpServers.Sum(mcpServer => mcpServer.Resources.Count)} resource(s) declared in \"{toolDefinitionsFile ?? "<no tool-definitions-file configured>"}\".");
                    // One collection of tools per mcp-server. Which of them a request uses is decided by the namespace
                    // of its route, so the same tool-name may exist in two mcp-servers without becoming ambiguous.
                    Dictionary<string, McpServerPrimitiveCollection<McpServerTool>> toolsOfTheMCPServers = new Dictionary<string, McpServerPrimitiveCollection<McpServerTool>>(StringComparer.OrdinalIgnoreCase);
                    // The resources are kept the same way. Only an mcp-server which declares at least one of them
                    // appears here: a server without an entry offers no resources at all and does not claim that
                    // capability of the protocol, which keeps a client from asking for a list which is empty by
                    // definition.
                    Dictionary<string, McpServerResourceCollection> resourcesOfTheMCPServers = new Dictionary<string, McpServerResourceCollection>(StringComparer.OrdinalIgnoreCase);
                    foreach (MCPServerDefinition mcpServer in mcpServers)
                    {
                        McpServerPrimitiveCollection<McpServerTool> tools = new McpServerPrimitiveCollection<McpServerTool>();
                        foreach (ToolDefinition definition in mcpServer.Tools)
                        {
                            tools.Add(new ScriptTool(businessLogicService, mcpServer.UrlNamespace, definition));
                        }
                        toolsOfTheMCPServers[mcpServer.UrlNamespace] = tools;
                        if (0 < mcpServer.Resources.Count)
                        {
                            McpServerResourceCollection resources = new McpServerResourceCollection();
                            foreach (ResourceDefinition definition in mcpServer.Resources)
                            {
                                resources.Add(new ScriptResource(businessLogicService, mcpServer.UrlNamespace, definition));
                            }
                            resourcesOfTheMCPServers[mcpServer.UrlNamespace] = resources;
                        }
                    }
                    functionalInformation.WebApplicationBuilder.Services.AddSingleton<IBusinessLogicService>(businessLogicService);
                    // The maintenance-route which reports the initialization-state asks this service, so it has to
                    // exist as soon as that route is enabled. It is registered under both interfaces, because the route
                    // resolves the non-generic one while the program calls the generic one to start the initialization.
                    functionalInformation.WebApplicationBuilder.Services.AddSingleton<IInitializationService<CommandlineParameter>, InitializationService>();
                    functionalInformation.WebApplicationBuilder.Services.AddSingleton<IInitializationService>(serviceProvider => serviceProvider.GetRequiredService<IInitializationService<CommandlineParameter>>());

                    // The whole MCP-protocol (the initialize-handshake, "tools/list", "tools/call" and the
                    // error-handling) is implemented by the SDK. This codeunit only hands it the tools.
                    functionalInformation.WebApplicationBuilder.Services.AddMcpServer()
                        .WithHttpTransport(options =>
                        {
                            // Stateless: every request is answered on its own, so no session-id has to be kept by the
                            // client. That makes the server usable with a plain http-request and is sufficient here,
                            // because the tools hold no state between two calls.
                            options.SessionMode = HttpServerSessionMode.Stateless;
                            // The tools are not registered once for the whole process: which of them exist is decided
                            // per request by the mcp-server its route belongs to. That is what makes several
                            // mcp-servers in one process possible, each with its own tools and its own clients.
                            options.ConfigureSessionOptions = (httpContext, serverOptions, cancellationToken) =>
                            {
                                MCPServerDefinition mcpServer = businessLogicService.GetMCPServerOfRoute(httpContext.Request.Path);
                                serverOptions.ToolCollection = mcpServer == null ? null : toolsOfTheMCPServers[mcpServer.UrlNamespace];
                                serverOptions.ResourceCollection = mcpServer != null && resourcesOfTheMCPServers.TryGetValue(mcpServer.UrlNamespace, out McpServerResourceCollection resources) ? resources : null;
                                return Task.CompletedTask;
                            };
                        });

                    functionalInformation.WebApplicationBuilder.Services.AddSingleton<ICommonRoutesInformation>(functionalInformation.PersistedAPIServerConfiguration.ApplicationSpecificConfiguration.CommonRoutesInformation);
                    functionalInformation.WebApplicationBuilder.Services.AddSingleton<IMaintenanceRoutesInformation>(functionalInformation.PersistedAPIServerConfiguration.ApplicationSpecificConfiguration.MaintenanceRoutesInformation);
                    functionalInformation.WebApplicationBuilder.Services.AddSingleton<IRequestLoggingConfiguration>(functionalInformation.PersistedAPIServerConfiguration.ApplicationSpecificConfiguration.ConfigurationForLoggingMiddleware);
                    functionalInformation.WebApplicationBuilder.Services.AddSingleton<IDRequestLoggingConfiguration>(functionalInformation.PersistedAPIServerConfiguration.ApplicationSpecificConfiguration.ConfigurationForDLoggingMiddleware);
                    functionalInformation.WebApplicationBuilder.Services.AddSingleton<IInitializationService, InitializationService>();
                    functionalInformation.WebApplicationBuilder.Services.AddSingleton<IInitializationService<CommandlineParameter>, InitializationService>();
                    functionalInformation.WebApplicationBuilder.Services.AddSingleton<IHealthCheck, HealthCheck>();
                };
                apiServerConfiguration.ConfigureWebApplication = (functionalInformationForWebApplication) =>
                {
                    this._Constants = apiServerConfiguration;
                    this._HostApplicationLifetime = functionalInformationForWebApplication.WebApplication.Services.GetService<IHostApplicationLifetime>();
                    this._BusinessLogicService = functionalInformationForWebApplication.WebApplication.Services.GetService<IBusinessLogicService>();
                    this._ApplicationConfiguration = functionalInformationForWebApplication.WebApplication.Services.GetService<CodeUnitSpecificConfiguration>();
                    functionalInformationForWebApplication.RunAsync = this.RunAsync;
                    IInitializationService<CommandlineParameter> initializationService = functionalInformationForWebApplication.WebApplication.Services.GetRequiredService<IInitializationService<CommandlineParameter>>();
                    functionalInformationForWebApplication.PreRun = () =>
                    {
                        initializationService.Initialize(apiServerConfiguration.CommandlineParameter);
                    };
                    // One endpoint per declared mcp-server; such an endpoint is what a MCP-client is configured with.
                    // They are mapped with their full route, because they are no controllers and therefore do not get
                    // the route-prefix of the API-server.
                    foreach (MCPServerDefinition mcpServer in this._BusinessLogicService.GetMCPServers())
                    {
                        functionalInformationForWebApplication.WebApplication.MapMcp(CodeUnitSpecificConstants.GetMCPRoute(mcpServer.UrlNamespace));
                    }
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
