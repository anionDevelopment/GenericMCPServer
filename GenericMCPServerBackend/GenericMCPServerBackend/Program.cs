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
            if (0 < commandlineArguments.Length && commandlineArguments[0] == CreateAccessTokenParameter.VerbName)
            {
                // This call does not start a server: it is what an administrator uses to create the first access at
                // all, so it is handled before anything of the server is set up.
                return CreateAccessToken(commandlineArguments);
            }
            // The file which declares the tools is read from the commandline-parameters, which are only available
            // once the initialization ran, so it is remembered here and used when the services are registered. The
            // same holds for the folder of the virtual environments of the python-tools.
            string toolDefinitionsFile = null;
            string virtualEnvironmentsFolder = null;
            string accessTokensFile = null;
            string rolesFile = null;
            this.IsRunning = true;
            int result = Tools.RunAPIServer<CommandlineParameter, CodeUnitSpecificConstants, CodeUnitSpecificConfiguration>(GeneralConstants.CodeUnitName, GeneralConstants.CodeUnitDescription, GRYLibrary.Core.Misc.Version3.Parse(GeneralConstants.CodeUnitVersion), GenericMCPServerBackend.Core.Misc.Utilities.GetEnvironmentTargetType(), GRYLibrary.Core.Misc.Utilities.GetExecutionMode(commandlineArguments), commandlineArguments, null, (apiServerConfiguration) =>
            {
                apiServerConfiguration.SetInitializationInformationAction = (initializationInformation) =>
                {
                    toolDefinitionsFile = initializationInformation.CommandlineParameter.InitialToolDefinitionsFile;
                    // The environments are generated content, so they belong below the data-folder of the server. The
                    // folder of the declarations can not hold them: it is usually mounted read-only.
                    virtualEnvironmentsFolder = string.IsNullOrWhiteSpace(initializationInformation.CommandlineParameter.InitialVirtualEnvironmentsFolder)
                        ? Path.Combine(initializationInformation.ApplicationConstants.GetDataFolder(), "VirtualEnvironments")
                        : initializationInformation.CommandlineParameter.InitialVirtualEnvironmentsFolder;
                    // The access-tokens belong to the configuration of the server, so they are kept in its
                    // configuration-folder unless another file was stated.
                    accessTokensFile = string.IsNullOrWhiteSpace(initializationInformation.CommandlineParameter.InitialAccessTokensFile)
                        ? AccessTokenService.GetDefaultFile(initializationInformation.ApplicationConstants.GetConfigurationFolder())
                        : initializationInformation.CommandlineParameter.InitialAccessTokensFile;
                    // The roles of the clients belong next to the tokens: a token says who is calling, a role says
                    // what that client may use.
                    rolesFile = string.IsNullOrWhiteSpace(initializationInformation.CommandlineParameter.InitialRolesFile)
                        ? RolesService.GetDefaultFile(initializationInformation.ApplicationConstants.GetConfigurationFolder())
                        : initializationInformation.CommandlineParameter.InitialRolesFile;
                    // The check of the token is the outer of the two custom middlewares: a request which is not
                    // allowed must not reach anything behind it.
                    initializationInformation.ApplicationConstants.CustomMiddlewares1.Add(typeof(AccessTokenValidatorMiddleware));
                    // The middleware which answers a failed tool-call with 502 has to wrap the endpoint of the
                    // protocol, so it is registered as the innermost custom middleware.
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
                    // does not exist yet, so that an administrator finds the place where the tokens belong.
                    functionalInformation.WebApplicationBuilder.Services.AddSingleton<IAccessTokenService>(new AccessTokenService(accessTokensFile));
                    functionalInformation.WebApplicationBuilder.Services.AddSingleton<IRolesService>(new RolesService(rolesFile));
                    BusinessLogicService businessLogicService = new BusinessLogicService(toolDefinitionsFile, virtualEnvironmentsFolder);
                    IReadOnlyList<MCPServerDefinition> mcpServers = businessLogicService.GetMCPServers();
                    this._Log.Log($"{mcpServers.Count} mcp-server(s) with {mcpServers.Sum(mcpServer => mcpServer.Tools.Count)} tool(s) declared in \"{toolDefinitionsFile ?? "<no tool-definitions-file configured>"}\".");
                    // One collection of tools per mcp-server. Which of them a request uses is decided by the namespace
                    // of its route, so the same tool-name may exist in two mcp-servers without becoming ambiguous.
                    Dictionary<string, McpServerPrimitiveCollection<McpServerTool>> toolsOfTheMCPServers = new Dictionary<string, McpServerPrimitiveCollection<McpServerTool>>(StringComparer.OrdinalIgnoreCase);
                    foreach (MCPServerDefinition mcpServer in mcpServers)
                    {
                        McpServerPrimitiveCollection<McpServerTool> tools = new McpServerPrimitiveCollection<McpServerTool>();
                        foreach (ToolDefinition definition in mcpServer.Tools)
                        {
                            tools.Add(new ScriptTool(businessLogicService, mcpServer.UrlNamespace, definition));
                        }
                        toolsOfTheMCPServers[mcpServer.UrlNamespace] = tools;
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
        /// Creates an access-token for a client and writes it to the standard-output.
        ///
        /// The token is written there and never into the log: a log is kept, copied and read by more people than the
        /// one who creates the token, and the token exists in a readable form only in this moment.
        /// </summary>
        /// <returns>0 if the token was created.</returns>
        private static int CreateAccessToken(string[] commandlineArguments)
        {
            int result = 1;
            new Parser(settings => settings.HelpWriter = Console.Error).ParseArguments<CreateAccessTokenParameter>(commandlineArguments).WithParsed(parameter =>
            {
                try
                {
                    string file = string.IsNullOrWhiteSpace(parameter.AccessTokensFile) ? AccessTokenService.GetDefaultFile(GetConfigurationFolder()) : parameter.AccessTokensFile;
                    IAccessTokenService accessTokenService = new AccessTokenService(file);
                    string token = accessTokenService.CreateAccessToken(parameter.Client, parameter.ValidForDays);
                    Console.WriteLine($"Created an access-token for the client \"{parameter.Client}\" which is valid for {parameter.ValidForDays} day(s) and which is stored in \"{accessTokenService.GetFile()}\".");
                    Console.WriteLine("The token is shown now and never again, because only its hash is stored:");
                    Console.WriteLine(token);
                    result = 0;
                }
                catch (Exception exception)
                {
                    Console.Error.WriteLine(exception.Message);
                    result = 1;
                }
            });
            return result;
        }

        /// <summary>
        /// The folder the server keeps its configuration in. The way to it is the one the server itself takes, so that
        /// a token which is created here is a token which the server reads.
        /// </summary>
        private static string GetConfigurationFolder()
        {
            // The mode is stated as a real run, because the folder of a real server is meant here and not the
            // temporary one of a test-run. "Configuration" is the name which GRYLibrary gives that folder.
            string baseFolder = GetBaseFolder.GetBaseFolderForProjectInCommonProjectStructure(GenericMCPServerBackend.Core.Misc.Utilities.GetEnvironmentTargetType(), GRYLibrary.Core.Misc.Utilities.GetValue(System.IO.Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)), RunProgram.Instance, false);
            return Path.Combine(baseFolder, "Configuration");
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
