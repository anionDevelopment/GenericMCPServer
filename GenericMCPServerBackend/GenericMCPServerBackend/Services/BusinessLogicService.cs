using GenericMCPServerBackend.Core.Misc;
using GenericMCPServerBackend.Core.Model;
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
    /// This class contains no tool: which tools the server offers is decided entirely by the tool-definitions-file an
    /// administrator points the server to. That file names, for every tool, its type, what implements it and the
    /// parameters it takes, so a new tool needs a new entry there - and no change of this codeunit.
    ///
    /// The values of the parameters are provided by the client and are therefore untrusted. They are handed over to
    /// the script in a way in which they can not be interpreted as anything but one argument each (see
    /// <see cref="RunScript"/>); whether a value has the format the tool expects is checked by the script itself, which
    /// keeps that knowledge next to the code which depends on it.
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

        private readonly IReadOnlyList<ToolDefinition> _ToolDefinitions;

        /// <summary>
        /// The folder the scripts are resolved against: the folder of the tool-definitions-file.
        /// </summary>
        private readonly string _ScriptFolder;

        private readonly VirtualEnvironmentService _VirtualEnvironmentService;

        private readonly WebRequestService _WebRequestService;

        private readonly CredentialsService _CredentialsService;

        /// <summary>
        /// Creates the business-logic for the tools which are declared in the given file.
        /// </summary>
        /// <param name="toolDefinitionsFile">The file which declares the tools, or null/empty if none was configured. In that case the server offers no tool at all, which is deliberate: a tool exists because an administrator declared it and not because this codeunit brings it along.</param>
        /// <param name="virtualEnvironmentsFolder">The folder which contains the virtual environments of the python-tools. It has to be writable, which is why it is not the folder of the declarations: that one is usually mounted read-only.</param>
        /// <exception cref="FileNotFoundException">Thrown when a file was configured but does not exist.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the file does not declare valid tools or when a declared credential is not available.</exception>
        public BusinessLogicService(string toolDefinitionsFile, string virtualEnvironmentsFolder)
        {
            this._VirtualEnvironmentService = new VirtualEnvironmentService(virtualEnvironmentsFolder);
            if (string.IsNullOrWhiteSpace(toolDefinitionsFile))
            {
                this._ToolDefinitions = new List<ToolDefinition>();
                this._ScriptFolder = AppContext.BaseDirectory;
            }
            else
            {
                string file = Path.GetFullPath(toolDefinitionsFile);
                this._ScriptFolder = Path.GetDirectoryName(file);
                this._ToolDefinitions = ToolDefinitionsReader.Read(file);
            }
            // The credentials are read after the declarations, because the declarations are what states which ones are
            // needed at all.
            this._CredentialsService = new CredentialsService(this._ToolDefinitions.SelectMany(definition => definition.Credentials.Values));
            this._WebRequestService = new WebRequestService(this._CredentialsService);
        }

        public string RemoveCredentialValuesFrom(string text)
        {
            return this._CredentialsService.RemoveValuesFrom(text);
        }

        public IReadOnlyList<ToolDefinition> GetToolDefinitions()
        {
            return this._ToolDefinitions;
        }

        /// <summary>
        /// Runs the given tool: what that means is decided by its type.
        /// </summary>
        /// <exception cref="KeyNotFoundException">Thrown when no tool with that name is declared.</exception>
        /// <exception cref="FileNotFoundException">Thrown when the script of the tool does not exist.</exception>
        /// <exception cref="PlatformNotSupportedException">Thrown when the tool can not run on this system.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the tool failed. The message contains what it reported.</exception>
        public string RunTool(string toolName, IReadOnlyDictionary<string, string> arguments)
        {
            ToolDefinition definition = this._ToolDefinitions.FirstOrDefault(tool => tool.ToolName == toolName);
            if (definition == null)
            {
                throw new KeyNotFoundException($"No tool with the name \"{toolName}\" is declared.");
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
        /// Runs the script of the given tool.
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
        private string RunScript(ToolDefinition definition, ToolType type, IReadOnlyDictionary<string, string> arguments)
        {
            string scriptFile = Path.GetFullPath(Path.Combine(this._ScriptFolder, definition.Calls));
            // The script has to stay inside the folder of the tool-definitions-file. The declaration itself comes from
            // an administrator and is therefore trusted, but this keeps a mistake in it (a path which points somewhere
            // else entirely) from turning into the start of an unrelated program.
            if (!scriptFile.StartsWith(this._ScriptFolder + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"The script \"{definition.Calls}\" of the tool \"{definition.ToolName}\" is not located inside \"{this._ScriptFolder}\".");
            }
            if (!File.Exists(scriptFile))
            {
                throw new FileNotFoundException($"The script \"{scriptFile}\" of the tool \"{definition.ToolName}\" does not exist.", scriptFile);
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
            string description = $"The script of the tool \"{definition.ToolName}\"";
            ProcessExecutionResult result = ProcessExecutor.Run(startInfo, ToolScriptTimeoutInSeconds, description);
            if (result.ExitCode != 0)
            {
                throw new InvalidOperationException($"The tool \"{definition.ToolName}\" failed (exit-code {result.ExitCode}): {result.ErrorOutput.Trim()}");
            }
            return result.Output;
        }

        /// <summary>
        /// Puts the credentials of the tool into the environment of the script.
        ///
        /// A script inherits the environment of the server, so the variables which the credentials are read from would
        /// otherwise be readable by every tool. They are therefore removed first and only the ones the tool declared
        /// are added afterwards, under the name the declaration gave them. Everything else of the environment (PATH,
        /// HOME, ...) stays untouched: a script needs it, and it is no secret of another tool.
        /// </summary>
        private void SetCredentials(ProcessStartInfo startInfo, ToolDefinition definition)
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
        /// The program which runs the script of the tool: for a python-tool the interpreter of its own virtual
        /// environment, for a shell-tool the shell of the system.
        /// </summary>
        /// <exception cref="PlatformNotSupportedException">Thrown when a shell-tool is run on a system which is not a linux-system.</exception>
        private string GetInterpreter(ToolDefinition definition, ToolType type)
        {
            if (type == ToolType.Shell)
            {
                if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    throw new PlatformNotSupportedException($"The tool \"{definition.ToolName}\" is of the type \"{ToolTypes.Shell}\", which is only supported on a linux-system.");
                }
                return ShellInterpreter;
            }
            return this._VirtualEnvironmentService.GetInterpreter(definition.ToolName, new List<string>(definition.Requirements));
        }
    }
}
