using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace GenericMCPServerBackend.Core.Misc
{
    /// <summary>
    /// A started program together with everything it starts itself, as one thing which can be ended in one go.
    ///
    /// This exists because <see cref="Process.Kill(bool)"/> alone does not carry the promise that a cancelled
    /// tool-call really stops what it started: it ends the program first and walks its children afterwards, and a
    /// child which has already ended is one whose own children it does not look for anymore. A python-tool on a
    /// windows-system is exactly that case - the interpreter of a virtual environment is a launcher which starts the
    /// real interpreter and takes it down together with itself - so what the script of the tool started survived the
    /// cancellation of its call while the call itself was reported as stopped.
    /// </summary>
    internal abstract class ProcessTree : IDisposable
    {
        /// <summary>
        /// Puts the given program into a tree which can be ended as one.
        /// </summary>
        /// <param name="process">The program which was already started.</param>
        /// <param name="description">What was started, as the beginning of a message. It is part of a failure, so that a reader learns which program it is about.</param>
        /// <exception cref="InvalidOperationException">Thrown when the program can not be placed into such a tree. That is no detail which can be passed over: a program which can not be ended as a whole is one whose cancellation would leave something behind.</exception>
        internal static ProcessTree Of(Process process, string description)
        {
            return RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? new TreeInAJobObject(process, description)
                : new TreeOfTheDescendants(process);
        }

        /// <summary>
        /// Ends the program together with everything it started. Calling it for a program which ended on its own in the
        /// meantime is no failure, because what this ends is the tree and not one process: the program can be gone
        /// while something it started is still running, which is the very case this type exists for.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when the tree could not be ended.</exception>
        internal abstract void Terminate();

        public abstract void Dispose();

        /// <summary>
        /// The tree on a windows-system: a job-object which the program is assigned to. Everything the program starts
        /// joins that job, so ending the job ends all of them at once, no matter in which order they ended on their own
        /// and no matter how deep they are nested.
        /// </summary>
        private sealed class TreeInAJobObject : ProcessTree
        {
            /// <summary>What the ended programs report as their exit-code. It is not 0, because they did not finish.</summary>
            private const uint ExitCodeOfAnEndedTree = 1;

            private readonly IntPtr _Job;

            internal TreeInAJobObject(Process process, string description)
            {
                this._Job = CreateJobObject(IntPtr.Zero, null);
                if (this._Job == IntPtr.Zero)
                {
                    throw new InvalidOperationException($"{description} could not be made stoppable: no job-object could be created.", new Win32Exception(Marshal.GetLastWin32Error()));
                }
                // A program which the started one launches before this assignment happened would not belong to the job.
                // The assignment is done immediately after the start and before anything was read from the program, so
                // that window is the time an interpreter needs to boot, which is long before it can start anything.
                if (!AssignProcessToJobObject(this._Job, process.Handle))
                {
                    Win32Exception reason = new Win32Exception(Marshal.GetLastWin32Error());
                    CloseHandle(this._Job);
                    throw new InvalidOperationException($"{description} could not be made stoppable: it could not be assigned to a job-object.", reason);
                }
            }

            internal override void Terminate()
            {
                if (!TerminateJobObject(this._Job, ExitCodeOfAnEndedTree))
                {
                    throw new InvalidOperationException("The started program and what it started could not be ended.", new Win32Exception(Marshal.GetLastWin32Error()));
                }
            }

            /// <remarks>
            /// The job is created without the limit which ends its programs when the last handle to it is closed, so
            /// this releases the job and ends nothing: a call which finished on its own leaves behind exactly what it
            /// left behind before this type existed.
            /// </remarks>
            public override void Dispose()
            {
                CloseHandle(this._Job);
            }

            [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
            private static extern IntPtr CreateJobObject(IntPtr securityAttributes, string name);

            [DllImport("kernel32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            private static extern bool AssignProcessToJobObject(IntPtr job, IntPtr process);

            [DllImport("kernel32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            private static extern bool TerminateJobObject(IntPtr job, uint exitCode);

            [DllImport("kernel32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            private static extern bool CloseHandle(IntPtr handle);
        }

        /// <summary>
        /// The tree everywhere else: the program and the programs which state it as their parent. This is what the
        /// runtime offers, and it holds there because the interpreter of a virtual environment is the interpreter
        /// itself and no launcher which ends before the programs below it do.
        /// </summary>
        private sealed class TreeOfTheDescendants : ProcessTree
        {
            private readonly Process _Process;

            internal TreeOfTheDescendants(Process process)
            {
                this._Process = process;
            }

            internal override void Terminate()
            {
                if (!this._Process.HasExited)
                {
                    this._Process.Kill(true);
                }
            }

            public override void Dispose()
            {
            }
        }
    }
}
