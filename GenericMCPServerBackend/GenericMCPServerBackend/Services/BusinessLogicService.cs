using GenericMCPServerBackend.Core.Model;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading.Tasks;

namespace GenericMCPServerBackend.Core.Services
{
    /// <summary>
    /// Implements the business-logic of this codeunit.
    ///
    /// This class contains no tool: which tools the server offers is decided entirely by the tool-definitions-file an
    /// administrator points the server to. That file names, for every tool, the script which implements it and the
    /// parameters it takes, so a new tool needs a new entry there and a script - and no change of this codeunit.
    ///
    /// The values of the parameters are provided by the client and are therefore untrusted. They are handed over to
    /// the script in a way in which they can not be interpreted as anything but one argument each (see
    /// <see cref="RunTool"/>); whether a value has the format the tool expects is checked by the script itself, which
    /// keeps that knowledge next to the code which depends on it.
    /// </summary>
    public class BusinessLogicService : IBusinessLogicService
    {
        /// <summary>
        /// The duration after which a script is aborted. It exists so that a script which waits for a foreign service
        /// forever can not block the server.
        /// </summary>
        private const int ToolScriptTimeoutInSeconds = 60;

        private readonly IReadOnlyList<ToolDefinition> _ToolDefinitions;

        /// <summary>
        /// The folder the scripts are resolved against: the folder of the tool-definitions-file.
        /// </summary>
        private readonly string _ScriptFolder;

        /// <summary>
        /// Creates the business-logic for the tools which are declared in the given file.
        /// </summary>
        /// <param name="toolDefinitionsFile">The file which declares the tools, or null/empty if none was configured. In that case the server offers no tool at all, which is deliberate: a tool exists because an administrator declared it and not because this codeunit brings it along.</param>
        /// <exception cref="FileNotFoundException">Thrown when a file was configured but does not exist. That is an error and not an empty list of tools, because a configured-but-missing file is a mistake which would otherwise stay unnoticed.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the file does not declare valid tools.</exception>
        public BusinessLogicService(string toolDefinitionsFile)
        {
            if (string.IsNullOrWhiteSpace(toolDefinitionsFile))
            {
                this._ToolDefinitions = new List<ToolDefinition>();
                this._ScriptFolder = AppContext.BaseDirectory;
                return;
            }
            string file = Path.GetFullPath(toolDefinitionsFile);
            if (!File.Exists(file))
            {
                throw new FileNotFoundException($"The tool-definitions-file \"{file}\" does not exist.", file);
            }
            this._ScriptFolder = Path.GetDirectoryName(file);
            ToolDefinitions definitions;
            try
            {
                definitions = JsonSerializer.Deserialize<ToolDefinitions>(File.ReadAllText(file), new JsonSerializerOptions() { ReadCommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
            }
            catch (JsonException exception)
            {
                throw new InvalidOperationException($"The tool-definitions-file \"{file}\" is not a valid json-document: {exception.Message}", exception);
            }
            this._ToolDefinitions = ValidateAndGetTools(definitions, file);
        }

        /// <summary>
        /// Checks that every declaration is complete and unambiguous.
        /// </summary>
        /// <param name="definitions">The deserialized content of the file.</param>
        /// <param name="file">The file, which is part of every message.</param>
        /// <returns>The declared tools.</returns>
        /// <exception cref="InvalidOperationException">Thrown when a declaration is incomplete or a name is used twice. This is checked when the server starts and not when a tool is called, so that a mistake in the configuration is visible immediately instead of only when somebody happens to call the affected tool.</exception>
        private static IReadOnlyList<ToolDefinition> ValidateAndGetTools(ToolDefinitions definitions, string file)
        {
            List<ToolDefinition> result = new List<ToolDefinition>(definitions?.Tools ?? new List<ToolDefinition>());
            foreach (ToolDefinition definition in result)
            {
                if (string.IsNullOrWhiteSpace(definition.ToolName))
                {
                    throw new InvalidOperationException($"The tool-definitions-file \"{file}\" contains a tool without a name.");
                }
                if (string.IsNullOrWhiteSpace(definition.Calls))
                {
                    throw new InvalidOperationException($"The tool \"{definition.ToolName}\" in \"{file}\" does not state which script it calls.");
                }
                if (definition.Parameters == null)
                {
                    definition.Parameters = new List<string>();
                }
                foreach (string parameter in definition.Parameters)
                {
                    if (string.IsNullOrWhiteSpace(parameter))
                    {
                        throw new InvalidOperationException($"The tool \"{definition.ToolName}\" in \"{file}\" has a parameter without a name.");
                    }
                }
            }
            string duplicate = result.GroupBy(definition => definition.ToolName).Where(group => 1 < group.Count()).Select(group => group.Key).FirstOrDefault();
            if (duplicate != null)
            {
                throw new InvalidOperationException($"The tool-definitions-file \"{file}\" declares the tool \"{duplicate}\" more than once.");
            }
            return result;
        }

        public IReadOnlyList<ToolDefinition> GetToolDefinitions()
        {
            return this._ToolDefinitions;
        }

        /// <summary>
        /// Runs the script of the given tool.
        ///
        /// The arguments are handed over without any escaping, and that is deliberate: no shell is involved at all
        /// (<see cref="ProcessStartInfo.UseShellExecute"/> is false) and every argument is added as its own entry of
        /// <see cref="ProcessStartInfo.ArgumentList"/>, so the operating-system passes each of them to the script as
        /// exactly one argument. A value can therefore not end a command and start another one, no matter which
        /// characters it contains - the quoting which would have to be escaped does not exist in this path. Every
        /// value is additionally passed as one single token in the form "--name=value", so a value which looks like an
        /// option can not be read as one.
        /// The program which is started has to stay a real executable: for a batch-file windows would parse the
        /// command-line again, which would reintroduce exactly the problem this avoids.
        /// </summary>
        /// <exception cref="KeyNotFoundException">Thrown when no tool with that name is declared.</exception>
        /// <exception cref="FileNotFoundException">Thrown when the script of the tool does not exist.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the script could not be started, ran into the timeout or ended with an exit-code which is not 0.</exception>
        public string RunTool(string toolName, IReadOnlyDictionary<string, string> arguments)
        {
            ToolDefinition definition = this._ToolDefinitions.FirstOrDefault(tool => tool.ToolName == toolName);
            if (definition == null)
            {
                throw new KeyNotFoundException($"No tool with the name \"{toolName}\" is declared.");
            }
            string scriptFile = Path.GetFullPath(Path.Combine(this._ScriptFolder, definition.Calls));
            // The script has to stay inside the folder of the tool-definitions-file. The declaration itself comes from
            // an administrator and is therefore trusted, but this keeps a mistake in it (a path which points somewhere
            // else entirely) from turning into the start of an unrelated program.
            if (!scriptFile.StartsWith(this._ScriptFolder + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"The script \"{definition.Calls}\" of the tool \"{toolName}\" is not located inside \"{this._ScriptFolder}\".");
            }
            if (!File.Exists(scriptFile))
            {
                throw new FileNotFoundException($"The script \"{scriptFile}\" of the tool \"{toolName}\" does not exist.", scriptFile);
            }
            ProcessStartInfo startInfo = new ProcessStartInfo()
            {
                // The name of the interpreter differs between the operating-systems: on a linux-system "python" is
                // usually not available while "python3" is, and on a windows-system it is the other way around.
                FileName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "python" : "python3",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = this._ScriptFolder,
            };
            startInfo.ArgumentList.Add(scriptFile);
            foreach (string parameter in definition.Parameters)
            {
                if (arguments.TryGetValue(parameter, out string value))
                {
                    startInfo.ArgumentList.Add($"--{parameter}={value}");
                }
            }
            using (Process process = Process.Start(startInfo))
            {
                if (process == null)
                {
                    throw new InvalidOperationException($"The script of the tool \"{toolName}\" could not be started.");
                }
                // Both streams are read while the process is still running: reading them one after the other would
                // block forever as soon as the script writes more into the other one than its buffer can hold.
                Task<string> outputTask = process.StandardOutput.ReadToEndAsync();
                Task<string> errorTask = process.StandardError.ReadToEndAsync();
                if (!process.WaitForExit(ToolScriptTimeoutInSeconds * 1000))
                {
                    process.Kill(true);
                    throw new InvalidOperationException($"The script of the tool \"{toolName}\" did not finish within {ToolScriptTimeoutInSeconds} seconds.");
                }
                string output = outputTask.Result;
                string errorOutput = errorTask.Result;
                if (process.ExitCode != 0)
                {
                    throw new InvalidOperationException($"The tool \"{toolName}\" failed (exit-code {process.ExitCode}): {errorOutput.Trim()}");
                }
                return output;
            }
        }
    }
}
