using GenericMCPServerBackend.Core.Misc;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ModelContextProtocol.Protocol;

namespace GenericMCPServerBackend.Tests.Testcases.Misc
{
    /// <summary>
    /// Covers the registry in which a tool-call is remembered while it runs, which is the place at which a tool-call
    /// and the cancellation of that tool-call meet: the two arrive as two http-requests which know nothing of each
    /// other, because the transport runs in the stateless mode.
    /// </summary>
    [TestClass]
    public class RunningToolCallsTests
    {
        private const string UrlNamespace = "test";

        private const string AnotherUrlNamespace = "another-test";

        private const string Client = "a-client";

        private const string AnotherClient = "another-client";

        private static ToolCallIdentity CreateIdentity(string urlNamespace, string? client, long requestId)
        {
            return new ToolCallIdentity(urlNamespace, client, new RequestId(requestId));
        }

        /// <remarks>
        /// The registry looks a call up by its identity, so two identities which name the same call have to be equal -
        /// also when they are compared as plain objects, which is what a collection of them does.
        /// </remarks>
        [TestMethod]
        [TestProperty(nameof(GRYLibrary.Core.Misc.TestKind), nameof(GRYLibrary.Core.Misc.TestKind.UnitTest))]
        public void TwoIdentitiesWhichNameTheSameCallAreEqual()
        {
            // arrange
            object identity = CreateIdentity(UrlNamespace, Client, 1);

            // act
            object theSameCall = CreateIdentity(UrlNamespace, Client, 1);

            // assert
            Assert.AreEqual(identity, theSameCall);
            Assert.AreEqual(identity.GetHashCode(), theSameCall.GetHashCode());
            Assert.AreNotEqual(identity, CreateIdentity(UrlNamespace, AnotherClient, 1));
            Assert.AreNotEqual(identity, CreateIdentity(AnotherUrlNamespace, Client, 1));
            Assert.AreNotEqual(identity, CreateIdentity(UrlNamespace, Client, 2));
            Assert.IsFalse(identity.Equals("no identity at all"));
        }

        [TestMethod]
        [TestProperty(nameof(GRYLibrary.Core.Misc.TestKind), nameof(GRYLibrary.Core.Misc.TestKind.UnitTest))]
        public void CancellingARunningCallCancelsWhatRunsIt()
        {
            // arrange
            RunningToolCalls runningToolCalls = new RunningToolCalls();
            using RunningToolCall runningToolCall = runningToolCalls.Start(CreateIdentity(UrlNamespace, Client, 1));

            // act
            bool wasRunning = runningToolCalls.Cancel(CreateIdentity(UrlNamespace, Client, 1));

            // assert
            Assert.IsTrue(wasRunning);
            Assert.IsTrue(runningToolCall.CancellationToken.IsCancellationRequested, "The token the program behind the call is run with has to be cancelled, because that is what stops that program.");
            Assert.IsTrue(runningToolCall.WasCancelled);
        }

        /// <remarks>
        /// That is what tells a cancelled call apart from one which failed: both end the same way from the point of
        /// view of whoever ran the program, while a client learns two different things from them.
        /// </remarks>
        [TestMethod]
        [TestProperty(nameof(GRYLibrary.Core.Misc.TestKind), nameof(GRYLibrary.Core.Misc.TestKind.UnitTest))]
        public void ACallWhichNobodyCancelledIsNoCancelledCall()
        {
            // arrange
            RunningToolCalls runningToolCalls = new RunningToolCalls();

            // act
            using RunningToolCall runningToolCall = runningToolCalls.Start(CreateIdentity(UrlNamespace, Client, 1));

            // assert
            Assert.IsFalse(runningToolCall.WasCancelled);
            Assert.IsFalse(runningToolCall.CancellationToken.IsCancellationRequested);
        }

        /// <remarks>
        /// A client cancels a call it does not have an answer for yet, so a cancellation which arrives after the call
        /// ended is the normal case of one which arrives too late and not a failure.
        /// </remarks>
        [TestMethod]
        [TestProperty(nameof(GRYLibrary.Core.Misc.TestKind), nameof(GRYLibrary.Core.Misc.TestKind.UnitTest))]
        public void ACancellationWhichNamesNoRunningCallStopsNothing()
        {
            // arrange
            RunningToolCalls runningToolCalls = new RunningToolCalls();

            // act
            bool wasRunning = runningToolCalls.Cancel(CreateIdentity(UrlNamespace, Client, 1));

            // assert
            Assert.IsFalse(wasRunning);
        }

        [TestMethod]
        [TestProperty(nameof(GRYLibrary.Core.Misc.TestKind), nameof(GRYLibrary.Core.Misc.TestKind.UnitTest))]
        public void ACallWhichEndedIsNotOneOfTheRunningCallsAnymore()
        {
            // arrange
            RunningToolCalls runningToolCalls = new RunningToolCalls();
            runningToolCalls.Start(CreateIdentity(UrlNamespace, Client, 1)).Dispose();

            // act
            bool wasRunning = runningToolCalls.Cancel(CreateIdentity(UrlNamespace, Client, 1));

            // assert
            Assert.IsFalse(wasRunning, "A call which ended must not be cancellable anymore, because there is nothing left to stop.");
        }

        /// <remarks>
        /// A request-id is only unambiguous within the client which chose it: two clients may both call their first
        /// request "1", so a cancellation of one of them must not reach the call of the other one.
        /// </remarks>
        [TestMethod]
        [TestProperty(nameof(GRYLibrary.Core.Misc.TestKind), nameof(GRYLibrary.Core.Misc.TestKind.UnitTest))]
        public void TheSameRequestIdOfTwoClientsNamesTwoCalls()
        {
            // arrange
            RunningToolCalls runningToolCalls = new RunningToolCalls();
            using RunningToolCall callOfTheOneClient = runningToolCalls.Start(CreateIdentity(UrlNamespace, Client, 1));
            using RunningToolCall callOfTheOtherClient = runningToolCalls.Start(CreateIdentity(UrlNamespace, AnotherClient, 1));

            // act
            runningToolCalls.Cancel(CreateIdentity(UrlNamespace, Client, 1));

            // assert
            Assert.IsTrue(callOfTheOneClient.WasCancelled);
            Assert.IsFalse(callOfTheOtherClient.WasCancelled, "A client must not be able to stop the call of another client.");
        }

        /// <remarks>
        /// The same reasoning as for two clients: an mcp-server is reached under its own route and knows nothing of the
        /// request-ids which a client uses at another one.
        /// </remarks>
        [TestMethod]
        [TestProperty(nameof(GRYLibrary.Core.Misc.TestKind), nameof(GRYLibrary.Core.Misc.TestKind.UnitTest))]
        public void TheSameRequestIdAtTwoMCPServersNamesTwoCalls()
        {
            // arrange
            RunningToolCalls runningToolCalls = new RunningToolCalls();
            using RunningToolCall callAtTheOneMCPServer = runningToolCalls.Start(CreateIdentity(UrlNamespace, Client, 1));
            using RunningToolCall callAtTheOtherMCPServer = runningToolCalls.Start(CreateIdentity(AnotherUrlNamespace, Client, 1));

            // act
            runningToolCalls.Cancel(CreateIdentity(UrlNamespace, Client, 1));

            // assert
            Assert.IsTrue(callAtTheOneMCPServer.WasCancelled);
            Assert.IsFalse(callAtTheOtherMCPServer.WasCancelled, "A cancellation at one mcp-server must not stop a call at another one.");
        }

        [TestMethod]
        [TestProperty(nameof(GRYLibrary.Core.Misc.TestKind), nameof(GRYLibrary.Core.Misc.TestKind.UnitTest))]
        public void ACancellationWhichNamesAnotherRequestIdStopsNothing()
        {
            // arrange
            RunningToolCalls runningToolCalls = new RunningToolCalls();
            using RunningToolCall runningToolCall = runningToolCalls.Start(CreateIdentity(UrlNamespace, Client, 1));

            // act
            bool wasRunning = runningToolCalls.Cancel(CreateIdentity(UrlNamespace, Client, 2));

            // assert
            Assert.IsFalse(wasRunning);
            Assert.IsFalse(runningToolCall.WasCancelled);
        }

        /// <remarks>
        /// An mcp-server which is declared as anonymous has no calling client, because it has no clients at all. Such
        /// a call is therefore identified without one, and it must not be confused with the call of a client which
        /// happens to use the same request-id at the same mcp-server.
        /// </remarks>
        [TestMethod]
        [TestProperty(nameof(GRYLibrary.Core.Misc.TestKind), nameof(GRYLibrary.Core.Misc.TestKind.UnitTest))]
        public void ACallWithoutAClientIsItsOwnCall()
        {
            // arrange
            RunningToolCalls runningToolCalls = new RunningToolCalls();
            using RunningToolCall callOfAnAnonymousCaller = runningToolCalls.Start(CreateIdentity(UrlNamespace, null, 1));
            using RunningToolCall callOfAClient = runningToolCalls.Start(CreateIdentity(UrlNamespace, Client, 1));

            // act
            runningToolCalls.Cancel(CreateIdentity(UrlNamespace, null, 1));

            // assert
            Assert.IsTrue(callOfAnAnonymousCaller.WasCancelled);
            Assert.IsFalse(callOfAClient.WasCancelled);
        }

        /// <remarks>
        /// A client which uses one request-id for two calls at the same time breaks the rule that a request-id is
        /// unambiguous; the call which was sent later is what that id names afterwards. The end of the call which was
        /// replaced must then not take the newer one out of the running calls, because that would make the one which
        /// is still running uncancellable.
        /// </remarks>
        [TestMethod]
        [TestProperty(nameof(GRYLibrary.Core.Misc.TestKind), nameof(GRYLibrary.Core.Misc.TestKind.UnitTest))]
        public void TheEndOfAReplacedCallLeavesTheCallWhichReplacedItCancellable()
        {
            // arrange
            RunningToolCalls runningToolCalls = new RunningToolCalls();
            RunningToolCall replacedCall = runningToolCalls.Start(CreateIdentity(UrlNamespace, Client, 1));
            using RunningToolCall callWhichReplacedIt = runningToolCalls.Start(CreateIdentity(UrlNamespace, Client, 1));

            // act
            replacedCall.Dispose();
            bool wasRunning = runningToolCalls.Cancel(CreateIdentity(UrlNamespace, Client, 1));

            // assert
            Assert.IsTrue(wasRunning);
            Assert.IsTrue(callWhichReplacedIt.WasCancelled);
        }
    }
}
