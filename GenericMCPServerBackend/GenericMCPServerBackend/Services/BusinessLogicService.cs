using GenericMCPServerBackend.Core.Constants;
using GenericMCPServerBackend.Core.Misc;
using GenericMCPServerBackend.Core.Model;
using GRYLibrary.Core.APIServer.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace GenericMCPServerBackend.Core.Services
{
    /// <summary>
    /// Implements the business-logic of this codeunit.
    ///
    /// This class contains no tool and no resource: which of them the server offers is decided entirely by the
    /// tool-definitions-file an administrator points the server to. That file names, for every tool, its type, what
    /// implements it and the parameters it takes, respectively, for every resource, the uri it is read by and what
    /// implements it - so a new one needs a new entry there and no change of this codeunit.
    ///
    /// The values of the parameters are provided by the client and are therefore untrusted. They are handed over to
    /// the script in a way in which they can not be interpreted as anything but one argument each (see
    /// <see cref="RunScript"/>); whether a value has the format the tool expects is checked by the script itself, which
    /// keeps that knowledge next to the code which depends on it.
    ///
    /// It is also the one place which decides who may reach what (see <see cref="ClientMayReachMCPServer"/> and the
    /// administration-methods below) and which administrates the clients, their roles and their tokens: a controller
    /// or a middleware which decided one of those things itself would be a decision the next one could forget, and
    /// this codeunit would no longer have a single place which states what "domain-logic" of it means.
    /// </summary>
    public class BusinessLogicService : IBusinessLogicService
    {
        /// <summary>
        /// The duration after which a script is aborted. It exists so that a script which waits for a foreign service
        /// forever can not block the server.
        /// </summary>
        private const int ToolScriptTimeoutInSeconds = 60;

        /// <summary>
        /// The program which runs a shell-script. Only a linux-system is supported for such a tool, so this is the
        /// shell of exactly that system.
        /// </summary>
        private const string ShellInterpreter = "/bin/sh";

        private readonly IReadOnlyList<MCPServerDefinition> _MCPServers;

        /// <summary>
        /// The folder the scripts are resolved against: the folder of the tool-definitions-file.
        /// </summary>
        private readonly string _ScriptFolder;

        private readonly VirtualEnvironmentService _VirtualEnvironmentService;

        private readonly WebRequestService _WebRequestService;

        private readonly CredentialsService _CredentialsService;

        private readonly IClientService _ClientService;

        private readonly IRolesService _RolesService;

        private readonly IAccessTokenService _AccessTokenService;

        private readonly IAdminTokenService _AdminTokenService;

        private readonly ITimeService _TimeService;

        /// <summary>
        /// Creates the business-logic for the tools which are declared in the given file, without the administration
        /// of clients, roles and tokens. This is what a testcase which is only about the tools uses; the server itself
        /// uses the constructor below, because it needs both.
        /// </summary>
        /// <param name="toolDefinitionsFile">The file which declares the tools, or null/empty if none was configured. In that case the server offers no tool at all, which is deliberate: a tool exists because an administrator declared it and not because this codeunit brings it along.</param>
        /// <param name="virtualEnvironmentsFolder">The folder which contains the virtual environments of the python-tools. It has to be writable, which is why it is not the folder of the declarations: that one is usually mounted read-only.</param>
        /// <exception cref="FileNotFoundException">Thrown when a file was configured but does not exist.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the file does not declare valid tools or when a declared credential is not available.</exception>
        public BusinessLogicService(string toolDefinitionsFile, string virtualEnvironmentsFolder)
            : this(toolDefinitionsFile, virtualEnvironmentsFolder, null, null, null, null, null)
        {
        }

        /// <summary>
        /// Creates the business-logic for the tools which are declared in the given file and for the administration
        /// which is backed by the given services.
        /// </summary>
        /// <param name="toolDefinitionsFile">The file which declares the tools, or null/empty if none was configured. In that case the server offers no tool at all, which is deliberate: a tool exists because an administrator declared it and not because this codeunit brings it along.</param>
        /// <param name="virtualEnvironmentsFolder">The folder which contains the virtual environments of the python-tools. It has to be writable, which is why it is not the folder of the declarations: that one is usually mounted read-only.</param>
        /// <param name="clientService">Where the clients and their administration-tokens are kept.</param>
        /// <param name="rolesService">Where the roles and their assignments are kept.</param>
        /// <param name="accessTokenService">Where the access-tokens of the clients are kept.</param>
        /// <param name="adminTokenService">Where the token of the administrator of this server is kept.</param>
        /// <param name="timeService">Where the current point in time comes from, so that a testcase can state which point in time it is.</param>
        /// <exception cref="FileNotFoundException">Thrown when a file was configured but does not exist.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the file does not declare valid tools or when a declared credential is not available.</exception>
        public BusinessLogicService(string toolDefinitionsFile, string virtualEnvironmentsFolder, IClientService clientService, IRolesService rolesService, IAccessTokenService accessTokenService, IAdminTokenService adminTokenService, ITimeService timeService)
        {
            this._VirtualEnvironmentService = new VirtualEnvironmentService(virtualEnvironmentsFolder);
            if (string.IsNullOrWhiteSpace(toolDefinitionsFile))
            {
                this._MCPServers = new List<MCPServerDefinition>();
                this._ScriptFolder = AppContext.BaseDirectory;
            }
            else
            {
                string file = Path.GetFullPath(toolDefinitionsFile);
                this._ScriptFolder = Path.GetDirectoryName(file);
                this._MCPServers = ToolDefinitionsReader.Read(file);
            }
            // The credentials are read after the declarations, because the declarations are what states which ones are
            // needed at all.
            this._CredentialsService = new CredentialsService(this._MCPServers.SelectMany(GetCallables).SelectMany(definition => definition.Credentials.Values));
            this._WebRequestService = new WebRequestService(this._CredentialsService);
            this._ClientService = clientService;
            this._RolesService = rolesService;
            this._AccessTokenService = accessTokenService;
            this._AdminTokenService = adminTokenService;
            this._TimeService = timeService;
        }

        /// <summary>
        /// Everything which the given mcp-server declares as something the server runs. A credential is assigned to a
        /// tool and to a resource in the same way, so both of them have to be known where the assigned credentials are
        /// collected.
        /// </summary>
        private static IEnumerable<CallableDefinition> GetCallables(MCPServerDefinition mcpServer)
        {
            return mcpServer.Tools.Cast<CallableDefinition>().Concat(mcpServer.Resources);
        }

        public string RemoveCredentialValuesFrom(string text)
        {
            return this._CredentialsService.RemoveValuesFrom(text);
        }


        public IReadOnlyList<MCPServerDefinition> GetMCPServers()
        {
            return this._MCPServers;
        }

        public MCPServerDefinition GetMCPServerOfRoute(string route)
        {
            return this._MCPServers.FirstOrDefault(mcpServer => string.Equals(CodeUnitSpecificConstants.GetMCPRoute(mcpServer.UrlNamespace), route?.TrimEnd('/'), StringComparison.OrdinalIgnoreCase));
        }

        public bool RequestIsFromAnAdministrator(string adminToken)
        {
            return this._AdminTokenService.Matches(adminToken);
        }

        public bool TryGetClientOfAdministrationToken(string clientAdministrationToken, out string client)
        {
            return this._ClientService.TryGetClientOfAdministrationToken(clientAdministrationToken, out client);
        }

        /// <summary>
        /// The token says which client is calling; whether that client may use this mcp-server is decided by its
        /// roles. A client which is in none of the declared roles is a known client without an access here.
        /// </summary>
        public bool ClientMayReachMCPServer(MCPServerDefinition mcpServer, string accessToken, out string client)
        {
            if (!this._AccessTokenService.TryGetClient(accessToken, out client))
            {
                return false;
            }
            IReadOnlyCollection<string> rolesOfTheClient = this._RolesService.GetRolesOf(client);
            return mcpServer.AuthorizedRoles.Any(role => rolesOfTheClient.Contains(role, StringComparer.OrdinalIgnoreCase));
        }

        public IReadOnlyCollection<ClientOverview> GetClients()
        {
            return this._ClientService.GetClients().Select(client => new ClientOverview(client, this._RolesService.GetRolesOf(client), this._AccessTokenService.GetAccessTokensOf(client).Count)).ToList();
        }

        public string CreateClient(string client)
        {
            return this._ClientService.CreateClient(client);
        }

        public string RenewClientAdministrationToken(string client)
        {
            return this._ClientService.RenewAdministrationToken(client);
        }

        /// <summary>
        /// The tokens and the roles of the client are removed as well: a client which does not exist anymore must not
        /// keep an access, and a client which is created again later must not inherit what its predecessor had.
        /// </summary>
        public bool DeleteClient(string client)
        {
            if (!this._ClientService.DeleteClient(client))
            {
                return false;
            }
            this._AccessTokenService.RemoveAccessTokensOf(client);
            this._RolesService.RemoveAllRolesOf(client);
            return true;
        }

        public IReadOnlyCollection<string> GetRoles()
        {
            return this._RolesService.GetRoles();
        }

        public bool CreateRole(string role)
        {
            return this._RolesService.CreateRole(role);
        }

        public bool DeleteRole(string role)
        {
            return this._RolesService.DeleteRole(role);
        }

        public IReadOnlyCollection<string> GetRolesOfClient(string client)
        {
            if (!this._ClientService.ClientExists(client))
            {
                return null;
            }
            return this._RolesService.GetRolesOf(client);
        }

        /// <exception cref="InvalidOperationException">Thrown when the client does not exist (the role itself is checked by <see cref="IRolesService.AssignRole"/>, which is also where this comes from).</exception>
        public bool AssignRole(string client, string role)
        {
            if (!this._ClientService.ClientExists(client))
            {
                throw new InvalidOperationException($"A client with the name \"{client}\" does not exist.");
            }
            return this._RolesService.AssignRole(client, role);
        }

        public bool RemoveRoleOfClient(string client, string role)
        {
            return this._RolesService.RemoveRoleOfClient(client, role);
        }

        public IReadOnlyList<AccessTokenOverview> GetAccessTokensOf(string client)
        {
            DateTime now = this._TimeService.GetCurrentTimeInUTCAsDateTimeOffset().UtcDateTime;
            return this._AccessTokenService.GetAccessTokensOf(client).Select(accessToken => new AccessTokenOverview(accessToken.Hash, accessToken.ValidUntil, accessToken.IsValidAt(now))).ToList();
        }

        public string CreateAccessToken(string client, int validForDays)
        {
            return this._AccessTokenService.CreateAccessToken(client, validForDays);
        }

        public bool RemoveAccessToken(string client, string hash)
        {
            return this._AccessTokenService.RemoveAccessToken(client, hash);
        }

        /// <summary>
        /// Runs the given tool: what that means is decided by its type.
        /// </summary>
        /// <exception cref="KeyNotFoundException">Thrown when no tool with that name is declared.</exception>
        /// <exception cref="FileNotFoundException">Thrown when the script of the tool does not exist.</exception>
        /// <exception cref="PlatformNotSupportedException">Thrown when the tool can not run on this system.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the tool failed. The message contains what it reported.</exception>
        public string RunTool(string urlNamespace, string toolName, IReadOnlyDictionary<string, string> arguments)
        {
            MCPServerDefinition mcpServer = this.GetMCPServer(urlNamespace);
            ToolDefinition definition = mcpServer.Tools.FirstOrDefault(tool => tool.ToolName == toolName);
            if (definition == null)
            {
                throw new KeyNotFoundException($"The mcp-server \"{urlNamespace}\" declares no tool with the name \"{toolName}\".");
            }
            // The type was already checked while the declarations were read, so it is known to be one of the possible
            // ones here.
            ToolTypes.TryParse(definition.Type, out ToolType type);
            if (type == ToolType.WebRequest)
            {
                return this._WebRequestService.Run(definition, arguments);
            }
            return this.RunScript(definition, type, arguments);
        }

        /// <summary>
        /// Reads the resource of the given mcp-server which is declared under the given uri.
        ///
        /// A resource is run like a tool, and nothing which a client sends decides what it answers: the uri is the
        /// whole request, and the script receives exactly that uri - which is what lets one script implement several
        /// resources.
        /// </summary>
        /// <exception cref="KeyNotFoundException">Thrown when no resource with that uri is declared.</exception>
        /// <exception cref="FileNotFoundException">Thrown when the script of the resource does not exist.</exception>
        /// <exception cref="PlatformNotSupportedException">Thrown when the resource can not be read on this system.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the resource could not be read. The message contains what was reported.</exception>
        public string ReadResource(string urlNamespace, string uri)
        {
            MCPServerDefinition mcpServer = this.GetMCPServer(urlNamespace);
            ResourceDefinition definition = mcpServer.Resources.FirstOrDefault(resource => string.Equals(resource.Uri, uri, StringComparison.Ordinal));
            if (definition == null)
            {
                throw new KeyNotFoundException($"The mcp-server \"{urlNamespace}\" declares no resource with the uri \"{uri}\".");
            }
            ToolTypes.TryParse(definition.Type, out ToolType type);
            if (type == ToolType.WebRequest)
            {
                return this._WebRequestService.ReadResource(definition);
            }
            return this.RunScript(definition, type, new Dictionary<string, string>() { { ResourceDefinition.UriParameterName, definition.Uri } });
        }

        /// <exception cref="KeyNotFoundException">Thrown when no mcp-server with that url-namespace is declared.</exception>
        private MCPServerDefinition GetMCPServer(string urlNamespace)
        {
            MCPServerDefinition result = this._MCPServers.FirstOrDefault(server => string.Equals(server.UrlNamespace, urlNamespace, StringComparison.OrdinalIgnoreCase));
            if (result == null)
            {
                throw new KeyNotFoundException($"No mcp-server with the url-namespace \"{urlNamespace}\" is declared.");
            }
            return result;
        }

        /// <summary>
        /// Runs the script of the given tool respectively of the given resource.
        ///
        /// The arguments are handed over without any escaping, and that is deliberate: no shell is involved at all
        /// (see <see cref="ProcessExecutor"/>) and every argument is added as its own entry of
        /// <see cref="ProcessStartInfo.ArgumentList"/>, so the operating-system passes each of them to the script as
        /// exactly one argument. A value can therefore not end a command and start another one, no matter which
        /// characters it contains - the quoting which would have to be escaped does not exist in this path. Every
        /// value is additionally passed as one single token in the form "--name=value", so a value which looks like an
        /// option can not be read as one.
        ///
        /// A shell-script is run by the shell itself and not by starting the file: the file comes from a folder which
        /// is usually mounted into the container, where it would not necessarily carry the permission to be executed.
        /// </summary>
        private string RunScript(CallableDefinition definition, ToolType type, IReadOnlyDictionary<string, string> arguments)
        {
            string scriptFile = Path.GetFullPath(Path.Combine(this._ScriptFolder, definition.Calls));
            // The script has to stay inside the folder of the tool-definitions-file. The declaration itself comes from
            // an administrator and is therefore trusted, but this keeps a mistake in it (a path which points somewhere
            // else entirely) from turning into the start of an unrelated program.
            if (!scriptFile.StartsWith(this._ScriptFolder + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"The script \"{definition.Calls}\" of the {definition.Kind} \"{definition.Name}\" is not located inside \"{this._ScriptFolder}\".");
            }
            if (!File.Exists(scriptFile))
            {
                throw new FileNotFoundException($"The script \"{scriptFile}\" of the {definition.Kind} \"{definition.Name}\" does not exist.", scriptFile);
            }
            ProcessStartInfo startInfo = new ProcessStartInfo()
            {
                FileName = this.GetInterpreter(definition, type),
                WorkingDirectory = this._ScriptFolder,
            };
            this.SetCredentials(startInfo, definition);
            startInfo.ArgumentList.Add(scriptFile);
            foreach (string parameter in definition.Parameters)
            {
                if (arguments.TryGetValue(parameter, out string value))
                {
                    startInfo.ArgumentList.Add($"--{parameter}={value}");
                }
            }
            string description = $"The script of the {definition.Kind} \"{definition.Name}\"";
            ProcessExecutionResult result = ProcessExecutor.Run(startInfo, ToolScriptTimeoutInSeconds, description);
            if (result.ExitCode != 0)
            {
                throw new InvalidOperationException($"The {definition.Kind} \"{definition.Name}\" failed (exit-code {result.ExitCode}): {result.ErrorOutput.Trim()}");
            }
            return result.Output;
        }

        /// <summary>
        /// Puts the credentials of the declaration into the environment of the script.
        ///
        /// A script inherits the environment of the server, so the variables which the credentials are read from would
        /// otherwise be readable by every tool. They are therefore removed first and only the ones the tool declared
        /// are added afterwards, under the name the declaration gave them. Everything else of the environment (PATH,
        /// HOME, ...) stays untouched: a script needs it, and it is no secret of another tool.
        /// </summary>
        private void SetCredentials(ProcessStartInfo startInfo, CallableDefinition definition)
        {
            foreach (string name in this._CredentialsService.GetNames())
            {
                startInfo.Environment.Remove(name);
                // The variable which states the file a credential is read from is removed as well: it is the way to
                // the secret, even though it is not the secret itself.
                startInfo.Environment.Remove(name + CredentialsService.NameSuffixForAFile);
            }
            foreach (KeyValuePair<string, string> credential in definition.Credentials)
            {
                startInfo.Environment[credential.Key] = this._CredentialsService.GetValue(credential.Value);
            }
        }

        /// <summary>
        /// The program which runs the script: for the type "python" the interpreter of the virtual environment which
        /// belongs to that declaration, for the type "shell" the shell of the system.
        /// </summary>
        /// <exception cref="PlatformNotSupportedException">Thrown when a shell-script is run on a system which is not a linux-system.</exception>
        private string GetInterpreter(CallableDefinition definition, ToolType type)
        {
            if (type == ToolType.Shell)
            {
                if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    throw new PlatformNotSupportedException($"The {definition.Kind} \"{definition.Name}\" is of the type \"{ToolTypes.Shell}\", which is only supported on a linux-system.");
                }
                return ShellInterpreter;
            }
            return this._VirtualEnvironmentService.GetInterpreter(definition.NameOfTheVirtualEnvironment, new List<string>(definition.Requirements));
        }
    }
}
