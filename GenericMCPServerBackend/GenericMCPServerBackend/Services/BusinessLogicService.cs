using ModelContextProtocol.Server;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace GenericMCPServerBackend.Core.Services
{
    /// <summary>
    /// Implements the business-logic of this codeunit.
    ///
    /// Every method which is marked with <see cref="McpServerToolAttribute"/> is offered as a tool of the MCP-server.
    /// The name of a tool, its description and the schema of its arguments are derived by the MCP-SDK from that
    /// attribute, from the <see cref="DescriptionAttribute"/> and from the signature of the method, so a new tool only
    /// needs a new method here and no change anywhere else. The class itself is marked with
    /// <see cref="McpServerToolTypeAttribute"/>, which is what makes the SDK look at its methods at all.
    ///
    /// Every tool of this server is implemented as a python-script; the method here only runs the script which belongs
    /// to it (see <see cref="RunToolScript"/>). That keeps the tools, which mainly talk to a foreign service, out of
    /// the codeunit-build: such a script can be changed without recompiling anything.
    ///
    /// The value of every argument of a tool is provided by the client and is therefore untrusted. Two things protect
    /// against that: it is handed over to the script in a way in which it can not be interpreted as anything but one
    /// argument (see <see cref="RunToolScript"/>), and the script itself checks it against the format it must have
    /// before it uses it. The check lives in the script and not here, so that it also applies when the script is
    /// called directly and so that the format is defined exactly once, next to the code which depends on it. A value
    /// which does not match makes the script end with an exit-code which is not 0, which turns into a failed
    /// tool-call.
    /// </summary>
    [McpServerToolType]
    public class BusinessLogicService : IBusinessLogicService
    {
        /// <summary>
        /// The folder inside the build-result which contains the scripts that implement tools.
        /// </summary>
        private const string ToolScriptFolderRelativePath = "Resources/Tools";

        /// <summary>
        /// The duration after which a script which implements a tool is aborted. It exists so that a script which
        /// waits for a foreign service forever can not block the server.
        /// </summary>
        private const int ToolScriptTimeoutInSeconds = 60;





        [McpServerTool(Name = "get_cve_details", ReadOnly = true, OpenWorld = true)]
        [Description("Returns the details of the given CVE (description, severity, references) from the national vulnerability database.")]
        public string GetCVEDetails([Description("The identifier of the CVE, for example \"CVE-2021-44228\".")] string cveIdentifier)
        {
            return RunToolScript("GetCVEDetails.py", $"--cve-identifier={cveIdentifier}");
        }

        [McpServerTool(Name = "get_available_maven_versions", ReadOnly = true, OpenWorld = true)]
        [Description("Returns the versions of the given maven-package which are available on maven-central.")]
        public string GetAvailableMavenVersions([Description("The group-id of the package, for example \"org.apache.logging.log4j\".")] string groupId, [Description("The artifact-id of the package, for example \"log4j-core\".")] string artifactId)
        {
            return RunToolScript("GetAvailableMavenVersions.py", $"--group-id={groupId}", $"--artifact-id={artifactId}");
        }

        [McpServerTool(Name = "get_available_nuget_versions", ReadOnly = true, OpenWorld = true)]
        [Description("Returns the versions of the given nuget-package which are available on nuget.org.")]
        public string GetAvailableNuGetVersions([Description("The id of the package, for example \"Newtonsoft.Json\".")] string packageId)
        {
            return RunToolScript("GetAvailableNuGetVersions.py", $"--package-id={packageId}");
        }

        [McpServerTool(Name = "get_available_pypi_versions", ReadOnly = true, OpenWorld = true)]
        [Description("Returns the versions of the given python-package which are available on pypi.")]
        public string GetAvailablePyPIVersions([Description("The name of the package, for example \"requests\".")] string packageName)
        {
            return RunToolScript("GetAvailablePyPIVersions.py", $"--package-name={packageName}");
        }

        [McpServerTool(Name = "get_available_npm_versions", ReadOnly = true, OpenWorld = true)]
        [Description("Returns the versions of the given npm-package which are available on the npm-registry.")]
        public string GetAvailableNPMVersions([Description("The name of the package, for example \"typescript\" or \"@angular/cli\".")] string packageName)
        {
            return RunToolScript("GetAvailableNPMVersions.py", $"--package-name={packageName}");
        }


        /// <summary>
        /// Runs the script which implements a tool and returns what it wrote to stdout.
        ///
        /// The arguments are handed over without any escaping, and that is deliberate: no shell is involved at all
        /// (<see cref="ProcessStartInfo.UseShellExecute"/> is false) and every argument is added as its own entry of
        /// <see cref="ProcessStartInfo.ArgumentList"/>, so the operating-system passes each of them to the script as
        /// exactly one argument. A value can therefore not end a command and start another one, no matter which
        /// characters it contains - the quoting which would have to be escaped does not exist in this path. Escaping
        /// the values instead would be weaker (an escaping which is forgotten or done twice is a bug of its own) and
        /// would additionally corrupt legitimate values.
        /// The program which is started has to stay a real executable: for a batch-file windows would parse the
        /// command-line again, which would reintroduce exactly the problem this avoids.
        /// </summary>
        /// <param name="scriptFileName">The name of the script inside the tool-script-folder.</param>
        /// <param name="arguments">The arguments which are passed to the script.</param>
        /// <returns>The output of the script.</returns>
        /// <exception cref="FileNotFoundException">Thrown when the script does not exist.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the script could not be started, ran into the timeout or ended with an exit-code which is not 0. The MCP-SDK turns this into a failed tool-call, so the reason reaches the client.</exception>
        private static string RunToolScript(string scriptFileName, params string[] arguments)
        {
            string scriptFile = Path.Combine(AppContext.BaseDirectory, ToolScriptFolderRelativePath.Replace("/", Path.DirectorySeparatorChar.ToString()), scriptFileName);
            if (!File.Exists(scriptFile))
            {
                throw new FileNotFoundException($"The script \"{scriptFile}\" which implements this tool does not exist.", scriptFile);
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
                WorkingDirectory = Path.GetDirectoryName(scriptFile),
            };
            startInfo.ArgumentList.Add(scriptFile);
            foreach (string argument in arguments)
            {
                startInfo.ArgumentList.Add(argument);
            }
            using (Process process = Process.Start(startInfo))
            {
                if (process == null)
                {
                    throw new InvalidOperationException($"The script \"{scriptFileName}\" could not be started.");
                }
                // Both streams are read while the process is still running: reading them one after the other would
                // block forever as soon as the script writes more into the other one than its buffer can hold.
                Task<string> outputTask = process.StandardOutput.ReadToEndAsync();
                Task<string> errorTask = process.StandardError.ReadToEndAsync();
                if (!process.WaitForExit(ToolScriptTimeoutInSeconds * 1000))
                {
                    process.Kill(true);
                    throw new InvalidOperationException($"The script \"{scriptFileName}\" did not finish within {ToolScriptTimeoutInSeconds} seconds.");
                }
                string output = outputTask.Result;
                string errorOutput = errorTask.Result;
                if (process.ExitCode != 0)
                {
                    throw new InvalidOperationException($"The script \"{scriptFileName}\" ended with the exit-code {process.ExitCode}: {errorOutput.Trim()}");
                }
                return output;
            }
        }
    }
}
