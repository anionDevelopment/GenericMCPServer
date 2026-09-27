using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;

namespace GenericMCPServerBackend.Core.Misc
{
    /// <summary>
    /// One tool-call which is running right now.
    ///
    /// It carries the token the program behind that call is run with, so that cancelling the call really stops that
    /// program instead of only letting nobody wait for it anymore. Disposing it takes the call out of the ones which
    /// can be cancelled: a cancellation which arrives after the call ended stops nothing.
    /// </summary>
    public sealed class RunningToolCall : IDisposable
    {
        private readonly RunningToolCalls _RunningToolCalls;

        private readonly ToolCallIdentity _Identity;

        private readonly CancellationTokenSource _CancellationTokenSource = new CancellationTokenSource();

        /// <summary>
        /// Guards the source against being cancelled and disposed at the same time: a cancellation of the client can
        /// arrive in exactly the moment in which the call ends, and a source which was disposed can not be cancelled
        /// anymore.
        /// </summary>
        private readonly object _Lock = new object();

        private bool _Ended;

        internal RunningToolCall(RunningToolCalls runningToolCalls, ToolCallIdentity identity)
        {
            this._RunningToolCalls = runningToolCalls;
            this._Identity = identity;
        }

        /// <summary>The token the program behind this call is run with. It is cancelled when the client cancels the call.</summary>
        public CancellationToken CancellationToken => this._CancellationTokenSource.Token;

        /// <summary>
        /// Whether this call was cancelled by the client.
        ///
        /// It is what tells a cancelled call apart from one which failed: both end the same way from the point of view
        /// of whoever ran the program, while a client learns two different things from them.
        /// </summary>
        public bool WasCancelled => this._CancellationTokenSource.IsCancellationRequested;

        internal void Cancel()
        {
            lock (this._Lock)
            {
                if (!this._Ended)
                {
                    this._CancellationTokenSource.Cancel();
                }
            }
        }

        public void Dispose()
        {
            this._RunningToolCalls.Remove(this._Identity, this);
            lock (this._Lock)
            {
                this._Ended = true;
                this._CancellationTokenSource.Dispose();
            }
        }
    }

    /// <summary>
    /// The tool-calls which are running right now, so that a client which cancels one of them can stop it.
    ///
    /// There is one of these for the whole process, and that is the entire point of it: the transport runs in the
    /// stateless mode, so a tool-call and the cancellation of that tool-call arrive as two http-requests which know
    /// nothing of each other - each of them is answered by an own instance of the server of the SDK, whose own
    /// cancellation-mechanism matches a cancellation to a request within one session and therefore never sees the two
    /// together. This registry is the place at which they meet.
    ///
    /// It decides nothing about a tool: it maps what a message of the protocol names (see
    /// <see cref="ToolCallIdentity"/>) to the token the program behind that call is run with, and what happens when
    /// that token is cancelled is decided where that program is run.
    /// </summary>
    public sealed class RunningToolCalls
    {
        private readonly ConcurrentDictionary<ToolCallIdentity, RunningToolCall> _RunningCalls = new ConcurrentDictionary<ToolCallIdentity, RunningToolCall>();

        /// <summary>
        /// Remembers that the given tool-call is running and returns what runs it. The result has to be disposed as
        /// soon as the call ended.
        /// </summary>
        /// <remarks>
        /// A call whose identity is the one of a call which is already running replaces that one, exactly like the SDK
        /// does it within a session: a client which uses one request-id for two calls at the same time breaks the rule
        /// that a request-id is unambiguous, and the call which was sent later is the one that id names afterwards.
        /// </remarks>
        public RunningToolCall Start(ToolCallIdentity identity)
        {
            RunningToolCall result = new RunningToolCall(this, identity);
            this._RunningCalls[identity] = result;
            return result;
        }

        /// <summary>
        /// Cancels the given tool-call, which stops the program behind it.
        /// </summary>
        /// <returns>Whether a call of that identity was running at all.</returns>
        public bool Cancel(ToolCallIdentity identity)
        {
            if (!this._RunningCalls.TryGetValue(identity, out RunningToolCall runningCall))
            {
                return false;
            }
            runningCall.Cancel();
            return true;
        }

        /// <summary>
        /// Takes the given call out of the running ones. Only that very call is removed and not whatever is registered
        /// under its identity: a client which reused a request-id has already replaced the entry, and removing the
        /// entry of the call which is still running would make that one uncancellable.
        /// </summary>
        internal void Remove(ToolCallIdentity identity, RunningToolCall runningCall)
        {
            this._RunningCalls.TryRemove(new KeyValuePair<ToolCallIdentity, RunningToolCall>(identity, runningCall));
        }
    }
}
