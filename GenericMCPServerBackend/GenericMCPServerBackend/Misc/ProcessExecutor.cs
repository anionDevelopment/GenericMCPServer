using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading.Tasks;

namespace GenericMCPServerBackend.Core.Misc
{
    /// <summary>
    /// What a program wrote and how it ended.
    /// </summary>
    internal sealed class ProcessExecutionResult
    {
        internal ProcessExecutionResult(int exitCode, string output, string errorOutput)
        {
            this.ExitCode = exitCode;
            this.Output = output;
            this.ErrorOutput = errorOutput;
        }

        internal int ExitCode { get; }

        internal string Output { get; }

        internal string ErrorOutput { get; }
    }

    /// <summary>
    /// Runs a program and waits for its result.
    ///
    /// Everything this codeunit starts goes through here, so that the properties which keep an argument an argument are
    /// set in one single place instead of at every call: no shell is involved
    /// (<see cref="ProcessStartInfo.UseShellExecute"/> is false), so the arguments of
    /// <see cref="ProcessStartInfo.ArgumentList"/> reach the program exactly as they were added.
    /// </summary>
    internal static class ProcessExecutor
    {
        /// <summary>
        /// Runs the given program and returns what it wrote and how it ended. Whether an exit-code which is not 0 is a
        /// failure is decided by the caller, because that differs between the things which are started here.
        /// </summary>
        /// <param name="startInfo">The program and its arguments. Its redirection-properties are set here.</param>
        /// <param name="timeoutInSeconds">The duration after which the program is aborted, so that a program which waits for something forever can not block the server.</param>
        /// <param name="description">What is started, as the beginning of a message ("The script of the tool \"x\""). It is part of every failure, so that a reader learns which of the started programs failed.</param>
        /// <exception cref="InvalidOperationException">Thrown when the program could not be started or ran into the timeout.</exception>
        internal static ProcessExecutionResult Run(ProcessStartInfo startInfo, int timeoutInSeconds, string description)
        {
            startInfo.RedirectStandardOutput = true;
            startInfo.RedirectStandardError = true;
            startInfo.UseShellExecute = false;
            startInfo.CreateNoWindow = true;
            Process process;
            try
            {
                process = Process.Start(startInfo);
            }
            catch (Win32Exception exception)
            {
                throw new InvalidOperationException($"{description} could not be started: {exception.Message}", exception);
            }
            if (process == null)
            {
                throw new InvalidOperationException($"{description} could not be started.");
            }
            using (process)
            {
                // Both streams are read while the process is still running: reading them one after the other would
                // block forever as soon as the program writes more into the other one than its buffer can hold.
                Task<string> outputTask = process.StandardOutput.ReadToEndAsync();
                Task<string> errorTask = process.StandardError.ReadToEndAsync();
                if (!process.WaitForExit(timeoutInSeconds * 1000))
                {
                    process.Kill(true);
                    throw new InvalidOperationException($"{description} did not finish within {timeoutInSeconds} seconds.");
                }
                return new ProcessExecutionResult(process.ExitCode, outputTask.Result, errorTask.Result);
            }
        }
    }
}
