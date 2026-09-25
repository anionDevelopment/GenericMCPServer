using GenericMCPServerBackend.Core.Misc;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace GenericMCPServerBackend.Core.Services
{
    /// <summary>
    /// Provides the python-interpreter a tool or a resource is run with.
    ///
    /// Every python-declaration gets a virtual environment of its own, named after that declaration. That is what
    /// makes the requirements of two of them independent of each other: without it, they would share the packages of
    /// the system and two of them which need two versions of the same package could not both work.
    ///
    /// The environment is built when it is needed and not when the server starts, so that a server which offers ten
    /// tools does not build ten environments before it answers anything.
    /// </summary>
    public sealed class VirtualEnvironmentService
    {
        /// <summary>
        /// The duration after which the creation of a virtual environment is aborted. It is much larger than the
        /// timeout of a tool, because the packages of the requirements are downloaded and installed here.
        /// </summary>
        private const int CreationTimeoutInSeconds = 600;

        /// <summary>
        /// The file inside the environment which states the requirements it was built with. It is written after the
        /// installation succeeded, so its existence means "this environment is complete and belongs to these
        /// requirements" - an environment whose creation failed is deleted and therefore never has it.
        /// </summary>
        private const string RequirementsFileName = "Requirements.txt";

        private readonly string _Folder;

        /// <summary>
        /// One lock per environment: two calls which use the same environment must not build it at the same time,
        /// while two calls which use two different ones have nothing to do with each other.
        /// </summary>
        private readonly ConcurrentDictionary<string, object> _Locks = new ConcurrentDictionary<string, object>();

        /// <param name="folder">The folder which contains the environments, one subfolder per declaration.</param>
        public VirtualEnvironmentService(string folder)
        {
            if (string.IsNullOrWhiteSpace(folder))
            {
                throw new ArgumentException("The folder for the virtual environments must be set.", nameof(folder));
            }
            this._Folder = Path.GetFullPath(folder);
        }

        /// <summary>
        /// Returns the python-interpreter which belongs to the given environment, and builds that environment if it
        /// does not exist yet or was built with other requirements than the declared ones.
        ///
        /// An environment which was built with other requirements is rebuilt and not reused: otherwise an administrator
        /// would change the requirements of a tool, nothing would happen, and the reason for that would be a folder
        /// which nobody looks at.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when the environment could not be created or the requirements could not be installed.</exception>
        /// <param name="nameOfTheEnvironment">The name of the environment, which is the name of its folder and which the declaration it belongs to states.</param>
        public string GetInterpreter(string nameOfTheEnvironment, IReadOnlyList<string> requirements)
        {
            string environmentFolder = Path.Combine(this._Folder, nameOfTheEnvironment);
            lock (this._Locks.GetOrAdd(nameOfTheEnvironment, _ => new object()))
            {
                if (!IsBuiltWith(environmentFolder, requirements))
                {
                    Create(environmentFolder, nameOfTheEnvironment, requirements);
                }
                return GetInterpreterFile(environmentFolder);
            }
        }

        /// <summary>
        /// The interpreter of an environment. Its location differs between the operating-systems.
        /// </summary>
        private static string GetInterpreterFile(string environmentFolder)
        {
            return RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? Path.Combine(environmentFolder, "Scripts", "python.exe")
                : Path.Combine(environmentFolder, "bin", "python");
        }

        /// <summary>
        /// The interpreter which builds an environment. The name differs between the operating-systems: on a
        /// linux-system "python" is usually not available while "python3" is, and on a windows-system it is the other
        /// way around.
        /// </summary>
        private static string GetSystemInterpreter()
        {
            return RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "python" : "python3";
        }

        private static bool IsBuiltWith(string environmentFolder, IReadOnlyList<string> requirements)
        {
            string requirementsFile = Path.Combine(environmentFolder, RequirementsFileName);
            if (!File.Exists(GetInterpreterFile(environmentFolder)) || !File.Exists(requirementsFile))
            {
                return false;
            }
            return File.ReadAllLines(requirementsFile).Where(line => !string.IsNullOrWhiteSpace(line)).SequenceEqual(requirements, StringComparer.Ordinal);
        }

        /// <summary>
        /// Builds the environment from scratch. An environment which could not be completed is removed again, so that
        /// a half-built one can not be mistaken for a working one by the next call.
        /// </summary>
        private static void Create(string environmentFolder, string nameOfTheEnvironment, IReadOnlyList<string> requirements)
        {
            EnsureDirectoryDoesNotExist(environmentFolder);
            Directory.CreateDirectory(environmentFolder);
            try
            {
                Run(GetSystemInterpreter(), new string[] { "-m", "venv", environmentFolder }, environmentFolder, $"The virtual environment \"{nameOfTheEnvironment}\"");
                string requirementsFile = Path.Combine(environmentFolder, RequirementsFileName);
                File.WriteAllLines(requirementsFile, requirements);
                if (0 < requirements.Count)
                {
                    // "--no-input" keeps pip from waiting for an answer nobody can give here, and
                    // "--disable-pip-version-check" keeps a note about a newer pip out of the output of a failure.
                    Run(GetInterpreterFile(environmentFolder), new string[] { "-m", "pip", "install", "--no-input", "--disable-pip-version-check", "--requirement", requirementsFile }, environmentFolder, $"The requirements of the virtual environment \"{nameOfTheEnvironment}\"");
                }
            }
            catch (Exception)
            {
                EnsureDirectoryDoesNotExist(environmentFolder);
                throw;
            }
        }

        private static void EnsureDirectoryDoesNotExist(string folder)
        {
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, true);
            }
        }

        /// <exception cref="InvalidOperationException">Thrown when the program failed. The message contains what the program reported, because that is what says why the environment could not be built.</exception>
        private static void Run(string program, IEnumerable<string> arguments, string workingDirectory, string description)
        {
            ProcessStartInfo startInfo = new ProcessStartInfo()
            {
                FileName = program,
                WorkingDirectory = workingDirectory,
            };
            foreach (string argument in arguments)
            {
                startInfo.ArgumentList.Add(argument);
            }
            ProcessExecutionResult result = ProcessExecutor.Run(startInfo, CreationTimeoutInSeconds, description);
            if (result.ExitCode != 0)
            {
                throw new InvalidOperationException($"{description} could not be created (exit-code {result.ExitCode}): {(string.IsNullOrWhiteSpace(result.ErrorOutput) ? result.Output : result.ErrorOutput).Trim()}");
            }
        }
    }
}
