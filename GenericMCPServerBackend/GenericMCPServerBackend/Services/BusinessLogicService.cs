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
using System.Threading;

namespace GenericMCPServerBackend.Core.Services
{
    /// <summary>
    /// Implements the business-logic of this codeunit.
    ///
    /// This class contains no tool, no resource and no prompt: which of them the server offers is decided entirely by
    /// the tool-definitions-file an administrator points the server to. That file names, for every tool, its type, what
    /// implements it and the parameters it takes, respectively, for every resource, the uri it is read by and what
    /// implements it, respectively, for every prompt, its name, the arguments it takes and what implements it - so a
    /// new one needs a new entry there and no change of this codeunit.
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

        /// <summary>
        /// The environment-variables which every started script keeps, no matter what it declares: a program needs them
        /// to run at all - its executables are found over the path, its locale and its temporary-folder are stated here
        /// - and none of them is a secret or a configuration of this server. Everything else of the environment of the
        /// server is removed, so that a variable which is set for the server does not reach a tool which did not
        /// declare it.
        /// </summary>
        private static readonly IReadOnlyList<string> EnvironmentVariablesWhichAreAlwaysKept = new List<string>() { "PATH", "HOME", "LANG", "LC_ALL", "TMPDIR", "TMP", "TZ" };

        private readonly IReadOnlyList<MCPServerDefinition> _MCPServers;

        /// <summary>
        /// The folder the scripts are resolved against: the folder of the tool-definitions-file.
        /// </summary>
        private readonly string _ScriptFolder;

        private readonly VirtualEnvironmentService _VirtualEnvironmentService;

        private readonly WebRequestService _WebRequestService;

        private readonly CredentialsService _CredentialsService;

        /// <summary>
        /// Serializes every access to the csv-files of the configuration (the clients, their roles, their access-tokens
        /// and the token of the administrator).
        ///
        /// It is taken by every method of this service which touches one of those files, which is the only place they
        /// are reached from while the server is running. That makes a whole operation atomic and not only the single
        /// read or write inside it, which is what the administration needs: creating a client asks whether its name is
        /// free and writes it afterwards, and deleting one writes into three files in a row. Without this, two requests
        /// which arrive at the same time could both pass the question and both write.
        ///
        /// It is static, because what is serialized is the access to the files and not the use of one instance.
        ///
        /// **A method which holds it must not call another method which takes it as well**: this is a semaphore and no
        /// monitor, so it is not re-entrant and such a call would wait for itself forever. A method of this service
        /// which needs something from another one reaches the services below directly, the way the ones here do.
        /// </summary>
        private static readonly SemaphoreSlim _AccessToTheConfigurationFiles = new SemaphoreSlim(1, 1);

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
        /// <param name="virtualEnvironmentsFolder">The folder which contains the virtual environments of the python-tools. It is not the folder of the declarations, because an environment is data of the server and not something an administrator edits.</param>
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
        /// <param name="virtualEnvironmentsFolder">The folder which contains the virtual environments of the python-tools. It is not the folder of the declarations, because an environment is data of the server and not something an administrator edits.</param>
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
        /// tool, to a resource and to a prompt in the same way, so every one of them has to be known where the assigned
        /// credentials are collected.
        /// </summary>
        /// <summary>
        /// Takes the exclusive access to the csv-files of the configuration until the returned object is disposed, so
        /// that an operation which consists of several reads and writes is one operation for everybody else as well.
        /// </summary>
        private static IDisposable ExclusiveAccessToTheConfigurationFiles()
        {
            _AccessToTheConfigurationFiles.Wait();
            return new ReleaseOfTheConfigurationFiles();
        }

        /// <summary>
        /// Releases the access to the csv-files of the configuration again, which is what ends the using-block of
        /// <see cref="ExclusiveAccessToTheConfigurationFiles"/>.
        /// </summary>
        private sealed class ReleaseOfTheConfigurationFiles : IDisposable
        {
            public void Dispose()
            {
                _AccessToTheConfigurationFiles.Release();
            }
        }

        private static IEnumerable<CallableDefinition> GetCallables(MCPServerDefinition mcpServer)
        {
            return mcpServer.Tools.Cast<CallableDefinition>().Concat(mcpServer.Resources).Concat(mcpServer.Prompts).Concat(mcpServer.Prompts.SelectMany(prompt => prompt.Completions.Values));
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
            using (ExclusiveAccessToTheConfigurationFiles())
            {
                return this._AdminTokenService.Matches(adminToken);
            }
        }

        public bool TryGetClientOfAdministrationToken(string clientAdministrationToken, out string client)
        {
            using (ExclusiveAccessToTheConfigurationFiles())
            {
                return this._ClientService.TryGetClientOfAdministrationToken(clientAdministrationToken, out client);
            }
        }

        /// <summary>
        /// The token says which client is calling; whether that client may use this mcp-server is decided by its
        /// roles. A client which is in none of the declared roles is a known client without an access here.
        /// </summary>
        public bool ClientMayReachMCPServer(MCPServerDefinition mcpServer, string accessToken, out string client)
        {
            using (ExclusiveAccessToTheConfigurationFiles())
            {
                if (!this._AccessTokenService.TryGetClient(accessToken, out client))
                {
                    return false;
                }
                IReadOnlyCollection<string> rolesOfTheClient = this._RolesService.GetRolesOf(client);
                return mcpServer.AuthorizedRoles.Any(role => rolesOfTheClient.Contains(role, StringComparer.OrdinalIgnoreCase));
            }
        }

        public IReadOnlyCollection<ClientOverview> GetClients()
        {
            using (ExclusiveAccessToTheConfigurationFiles())
            {
                return this._ClientService.GetClients().Select(client => new ClientOverview(client, this._RolesService.GetRolesOf(client), this._AccessTokenService.GetAccessTokensOf(client).Count)).ToList();
            }
        }

        /// <summary>
        /// Whether the name is still free is decided here and not where the clients are stored: it is a rule about the
        /// domain (a name identifies exactly one client) and not a property of the file the clients happen to lie in.
        /// It is checked in one place only, so a caller receives one failure with one reason and not two of them.
        /// </summary>
        public string CreateClient(string client)
        {
            using (ExclusiveAccessToTheConfigurationFiles())
            {
                if (this._ClientService.ClientExists(client))
                {
                    // The existing client is not replaced: that would hand out a new administration-token for a name
                    // which somebody else already holds and would take the access of that client away, without anybody
                    // stating it.
                    throw new InvalidOperationException($"A client with the name \"{client}\" already exists.");
                }
                return this._ClientService.CreateClient(client);
            }
        }

        /// <summary>
        /// Whether the client exists is decided here, for the same reason it is decided here when a client is created:
        /// it is a rule about the domain and not a property of the file the clients lie in.
        /// </summary>
        public string RenewClientAdministrationToken(string client)
        {
            using (ExclusiveAccessToTheConfigurationFiles())
            {
                if (!this._ClientService.ClientExists(client))
                {
                    throw new InvalidOperationException($"A client with the name \"{client}\" does not exist.");
                }
                return this._ClientService.RenewAdministrationToken(client);
            }
        }

        /// <summary>
        /// The tokens and the roles of the client are removed as well: a client which does not exist anymore must not
        /// keep an access, and a client which is created again later must not inherit what its predecessor had.
        /// </summary>
        public bool DeleteClient(string client)
        {
            using (ExclusiveAccessToTheConfigurationFiles())
            {
                if (!this._ClientService.DeleteClient(client))
                {
                    return false;
                }
                this._AccessTokenService.RemoveAccessTokensOf(client);
                this._RolesService.RemoveAllRolesOf(client);
                return true;
            }
        }

        public IReadOnlyCollection<string> GetRoles()
        {
            using (ExclusiveAccessToTheConfigurationFiles())
            {
                return this._RolesService.GetRoles();
            }
        }

        public bool CreateRole(string role)
        {
            using (ExclusiveAccessToTheConfigurationFiles())
            {
                return this._RolesService.CreateRole(role);
            }
        }

        public bool DeleteRole(string role)
        {
            using (ExclusiveAccessToTheConfigurationFiles())
            {
                return this._RolesService.DeleteRole(role);
            }
        }

        public IReadOnlyCollection<string> GetRolesOfClient(string client)
        {
            using (ExclusiveAccessToTheConfigurationFiles())
            {
                if (!this._ClientService.ClientExists(client))
                {
                    return null;
                }
                return this._RolesService.GetRolesOf(client);
            }
        }

        /// <exception cref="InvalidOperationException">Thrown when the client does not exist (the role itself is checked by <see cref="IRolesService.AssignRole"/>, which is also where this comes from).</exception>
        public bool AssignRole(string client, string role)
        {
            using (ExclusiveAccessToTheConfigurationFiles())
            {
                if (!this._ClientService.ClientExists(client))
                {
                    throw new InvalidOperationException($"A client with the name \"{client}\" does not exist.");
                }
                return this._RolesService.AssignRole(client, role);
            }
        }

        public bool RemoveRoleOfClient(string client, string role)
        {
            using (ExclusiveAccessToTheConfigurationFiles())
            {
                return this._RolesService.RemoveRoleOfClient(client, role);
            }
        }

        public IReadOnlyList<AccessTokenOverview> GetAccessTokensOf(string client)
        {
            DateTime now = this._TimeService.GetCurrentTimeInUTCAsDateTimeOffset().UtcDateTime;
            using (ExclusiveAccessToTheConfigurationFiles())
            {
                return this._AccessTokenService.GetAccessTokensOf(client).Select(accessToken => new AccessTokenOverview(accessToken.Hash, accessToken.ValidUntil, accessToken.IsValidAt(now))).ToList();
            }
        }

        public string CreateAccessToken(string client, int validForDays)
        {
            using (ExclusiveAccessToTheConfigurationFiles())
            {
                return this._AccessTokenService.CreateAccessToken(client, validForDays);
            }
        }

        public bool RemoveAccessToken(string client, string hash)
        {
            using (ExclusiveAccessToTheConfigurationFiles())
            {
                return this._AccessTokenService.RemoveAccessToken(client, hash);
            }
        }

        /// <summary>
        /// Runs the given tool: what that means is decided by its type.
        /// </summary>
        /// <param name="cancellationToken">Stops the tool while it runs. This is what a client which cancelled its call reaches the running script respectively the running web-request with.</param>
        /// <exception cref="KeyNotFoundException">Thrown when no tool with that name is declared.</exception>
        /// <exception cref="FileNotFoundException">Thrown when the script of the tool does not exist.</exception>
        /// <exception cref="PlatformNotSupportedException">Thrown when the tool can not run on this system.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the tool failed. The message contains what it reported.</exception>
        /// <exception cref="OperationCanceledException">Thrown when the given token was cancelled. What the tool was running is stopped before that happens.</exception>
        public string RunTool(string urlNamespace, string toolName, IReadOnlyDictionary<string, string> arguments, CancellationToken cancellationToken)
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
                return this._WebRequestService.Run(definition, arguments, cancellationToken);
            }
            return this.RunScript(definition, type, arguments, cancellationToken);
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
            // A read of a resource is not cancellable: the protocol lets a client cancel a request it sent, and this
            // server stops what a cancelled tool-call runs - a resource, a prompt and a completion are answered by a
            // program which is asked once and which no client can name in a cancellation here.
            return this.RunScript(definition, type, new Dictionary<string, string>() { { ResourceDefinition.UriParameterName, definition.Uri } }, CancellationToken.None);
        }

        /// <summary>
        /// Builds the prompt of the given mcp-server with the given arguments.
        ///
        /// A prompt is run exactly like a tool - the values of its arguments come from the client and are handed to the
        /// script in the same way - and what the script writes is the text of the prompt.
        /// </summary>
        /// <exception cref="KeyNotFoundException">Thrown when no prompt with that name is declared.</exception>
        /// <exception cref="FileNotFoundException">Thrown when the script of the prompt does not exist.</exception>
        /// <exception cref="PlatformNotSupportedException">Thrown when the prompt can not be built on this system.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the prompt could not be built. The message contains what was reported.</exception>
        public string GetPrompt(string urlNamespace, string promptName, IReadOnlyDictionary<string, string> arguments)
        {
            MCPServerDefinition mcpServer = this.GetMCPServer(urlNamespace);
            PromptDefinition definition = mcpServer.Prompts.FirstOrDefault(prompt => prompt.PromptName == promptName);
            if (definition == null)
            {
                throw new KeyNotFoundException($"The mcp-server \"{urlNamespace}\" declares no prompt with the name \"{promptName}\".");
            }
            // A prompt is never of the type "web-request" (see ToolDefinitionsReader), so it always runs a script.
            ToolTypes.TryParse(definition.Type, out ToolType type);
            return this.RunScript(definition, type, arguments, CancellationToken.None);
        }

        /// <summary>
        /// Returns the values which the argument of the given prompt can have, as the script which is declared for
        /// that argument states them.
        ///
        /// The script is run exactly like the one of a tool and is told which argument it is asked about and which
        /// value was entered so far; every non-empty line it writes is one value.
        ///
        /// An argument which declares no completion is answered with no value at all, and so is a prompt which is not
        /// declared: "which values are possible" is a question a client may ask about anything, and "none are known
        /// here" is an answer to it and not a failure.
        /// </summary>
        /// <exception cref="KeyNotFoundException">Thrown when no mcp-server with that url-namespace is declared.</exception>
        /// <exception cref="FileNotFoundException">Thrown when the script of the completion does not exist.</exception>
        /// <exception cref="PlatformNotSupportedException">Thrown when the completion can not run on this system.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the completion failed. The message contains what it reported.</exception>
        public IReadOnlyList<string> GetCompletions(string urlNamespace, string promptName, string argumentName, string argumentValue)
        {
            MCPServerDefinition mcpServer = this.GetMCPServer(urlNamespace);
            PromptDefinition prompt = mcpServer.Prompts.FirstOrDefault(declaredPrompt => declaredPrompt.PromptName == promptName);
            if (prompt == null || !prompt.Completions.TryGetValue(argumentName, out CompletionDefinition definition))
            {
                return new List<string>();
            }
            // A completion is never of the type "web-request" (see ToolDefinitionsReader), so it always runs a script.
            ToolTypes.TryParse(definition.Type, out ToolType type);
            string output = this.RunScript(definition, type, new Dictionary<string, string>()
            {
                { CompletionDefinition.ArgumentParameterName, argumentName },
                { CompletionDefinition.ValueParameterName, argumentValue },
            }, CancellationToken.None);
            return ReadValues(output);
        }

        /// <summary>
        /// Reads the values which the script of a completion wrote: one per line.
        ///
        /// A line-based answer is what a script can write without knowing anything about this server, and the lines
        /// are trimmed and empty ones are dropped, because a trailing newline is what every usual way of writing a
        /// line leaves behind and not a value which happens to be empty.
        /// </summary>
        private static IReadOnlyList<string> ReadValues(string output)
        {
            List<string> result = new List<string>();
            foreach (string line in output.Split('\n'))
            {
                string value = line.Trim();
                if (!string.IsNullOrEmpty(value))
                {
                    result.Add(value);
                }
            }
            return result;
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
        /// Runs the script of the given tool, of the given resource respectively of the given prompt.
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
        /// <param name="cancellationToken">Ends the script while it runs. Only a tool-call passes one which can be
        /// cancelled; a resource, a prompt and a completion pass none, because a client cancels a tool-call and nothing
        /// else here.</param>
        private string RunScript(CallableDefinition definition, ToolType type, IReadOnlyDictionary<string, string> arguments, CancellationToken cancellationToken)
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
            this.SetEnvironment(startInfo, definition);
            startInfo.ArgumentList.Add(scriptFile);
            foreach (string parameter in definition.Parameters)
            {
                if (arguments.TryGetValue(parameter, out string value))
                {
                    startInfo.ArgumentList.Add($"--{parameter}={value}");
                }
            }
            string description = $"The script of the {definition.Kind} \"{definition.Name}\"";
            ProcessExecutionResult result = ProcessExecutor.Run(startInfo, ToolScriptTimeoutInSeconds, description, cancellationToken);
            if (result.ExitCode != 0)
            {
                throw new InvalidOperationException($"The {definition.Kind} \"{definition.Name}\" failed (exit-code {result.ExitCode}): {result.ErrorOutput.Trim()}");
            }
            return result.Output;
        }

        /// <summary>
        /// Builds the environment of the script of the given declaration.
        ///
        /// A started script does not inherit the environment of the server: a variable which an operator sets for the
        /// server (in the compose-file, for example) would otherwise be readable by every tool, including the ones
        /// which have nothing to do with it. The environment is therefore emptied and rebuilt from exactly the sources
        /// a declaration states: the few operating-system-variables a program needs to run at all
        /// (<see cref="EnvironmentVariablesWhichAreAlwaysKept"/>), the environment-variables the declaration states as
        /// mandatory, the ones it states as optional which are actually available, and the credentials.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when a mandatory environment-variable is not set or carries no value with a visible character.</exception>
        private void SetEnvironment(ProcessStartInfo startInfo, CallableDefinition definition)
        {
            // Reading the environment the first time fills it from the server-process; it is emptied so that only what
            // is added below reaches the script and nothing the server happens to be configured with.
            startInfo.Environment.Clear();
            foreach (string name in EnvironmentVariablesWhichAreAlwaysKept)
            {
                string value = Environment.GetEnvironmentVariable(name);
                if (value != null)
                {
                    startInfo.Environment[name] = value;
                }
            }
            foreach (string name in definition.MandatoryEnvironmentVariables)
            {
                string value = Environment.GetEnvironmentVariable(name);
                // A mandatory variable has to carry an actual value. A variable which is missing or holds only
                // whitespace is one the script can not work with, so this is said instead of running the script with a
                // value which is not there.
                if (string.IsNullOrWhiteSpace(value))
                {
                    throw new InvalidOperationException($"The {definition.Kind} \"{definition.Name}\" declares the mandatory environment-variable \"{name}\", but it is not set or carries no value.");
                }
                startInfo.Environment[name] = value;
            }
            foreach (string name in definition.OptionalEnvironmentVariables)
            {
                string value = Environment.GetEnvironmentVariable(name);
                // An optional variable is handed over when it carries a value and left out otherwise; its absence does
                // not keep the script from running.
                if (!string.IsNullOrWhiteSpace(value))
                {
                    startInfo.Environment[name] = value;
                }
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
