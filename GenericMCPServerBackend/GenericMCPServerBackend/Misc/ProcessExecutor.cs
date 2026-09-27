using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading;
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
        /// <param name="cancellationToken">Ends the program before it is finished and before its timeout is reached. This is what makes a tool-call which a client cancelled really stop instead of only letting nobody wait for it anymore.</param>
        /// <exception cref="InvalidOperationException">Thrown when the program could not be started or ran into the timeout.</exception>
        /// <exception cref="OperationCanceledException">Thrown when the given token was cancelled. The program is ended before that happens.</exception>
        internal static ProcessExecutionResult Run(ProcessStartInfo startInfo, int timeoutInSeconds, string description, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
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
            using (ProcessTree tree = PutIntoATreeOrEndIt(process, description))
            {
                // Both streams are read while the process is still running: reading them one after the other would
                // block forever as soon as the program writes more into the other one than its buffer can hold.
                Task<string> outputTask = process.StandardOutput.ReadToEndAsync();
                Task<string> errorTask = process.StandardError.ReadToEndAsync();
                // The timeout and the cancellation end the wait in the same way, because the program has to be ended in
                // both cases: it is what keeps a program which waits forever from blocking the server, and it is what
                // makes a cancelled call stop the program instead of leaving it behind.
                using (CancellationTokenSource endOfTheWait = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
                {
                    endOfTheWait.CancelAfter(TimeSpan.FromSeconds(timeoutInSeconds));
                    try
                    {
                        process.WaitForExitAsync(endOfTheWait.Token).GetAwaiter().GetResult();
                    }
                    catch (OperationCanceledException)
                    {
                        // The whole tree is ended, because a program which started another one would otherwise leave
                        // that one behind, and a script of a tool is exactly the kind of program which starts another
                        // one.
                        tree.Terminate();
                        // Which of the two ended the wait is what the caller learns: a cancellation is a decision of
                        // the client and no failure of the program, so it is not reported as one.
                        cancellationToken.ThrowIfCancellationRequested();
                        throw new InvalidOperationException($"{description} did not finish within {timeoutInSeconds} seconds.");
                    }
                }
                return new ProcessExecutionResult(process.ExitCode, outputTask.Result, errorTask.Result);
            }
        }

        /// <summary>
        /// Puts the started program into a tree which can be ended as one, and ends the program itself when that is not
        /// possible: a program which nothing would be able to stop later must not be left running now.
        /// </summary>
        private static ProcessTree PutIntoATreeOrEndIt(Process process, string description)
        {
            try
            {
                return ProcessTree.Of(process, description);
            }
            catch (Exception)
            {
                if (!process.HasExited)
                {
                    process.Kill(true);
                }
                throw;
            }
        }
    }
}
