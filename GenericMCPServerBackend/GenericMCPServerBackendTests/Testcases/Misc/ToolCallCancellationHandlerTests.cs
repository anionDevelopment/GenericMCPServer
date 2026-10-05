using GenericMCPServerBackend.Core.Misc;
using GRYLibrary.Core.Logging.GRYLogger;
using GRYLibrary.Core.Logging.GRYLogger.ConcreteLogTargets;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ModelContextProtocol.Protocol;
using System;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading;

namespace GenericMCPServerBackend.Tests.Testcases.Misc
{
    /// <summary>
    /// Covers what a "notifications/cancelled" of a client does: it names a request-id, and which call that is follows
    /// from the mcp-server and the client of the request the notification arrived with.
    /// </summary>
    [TestClass]
    public class ToolCallCancellationHandlerTests
    {
        private const string UrlNamespace = "test";

        private const string AnotherUrlNamespace = "another-test";

        private const string Client = "a-client";

        private const string AnotherClient = "another-client";

        private static ToolCallIdentity CreateIdentity(string urlNamespace, string? client, long requestId)
        {
            return new ToolCallIdentity(urlNamespace, client, new RequestId(requestId));
        }

        /// <summary>
        /// A cancellation with the given parameters, which arrive as the json they arrive as on the wire: the handler
        /// reads them itself, so a testcase which handed over an object which was built with the types of the sdk would
        /// skip exactly that step.
        /// </summary>
        /// <param name="parameters">The parameters of the notification as json, or null for a notification which carries none.</param>
        private static JsonRpcNotification CreateNotification(string? parameters)
        {
            return new JsonRpcNotification()
            {
                Method = NotificationMethods.CancelledNotification,
                Params = parameters == null ? null : JsonNode.Parse(parameters),
            };
        }

        private static string CreateFolder()
        {
            return Path.Combine(Path.GetTempPath(), "GenericMCPServerBackendTests", Guid.NewGuid().ToString());
        }

        /// <summary>
        /// Creates a log which writes into a file of the given folder, so that a testcase can read what was written.
        /// </summary>
        private static GRYLog CreateLog(string folder)
        {
            Directory.CreateDirectory(folder);
            // This overload is the one which also sets the base-path of the log; the file-target resolves its file
            // against it, so a log which was created without it writes nowhere.
            GRYLog log = GRYLog.Create(Path.Combine(folder, "Log.log"), null, folder);
            log.Configuration.LogTargets.OfType<LogFile>().Single().Enabled = true;
            return log;
        }

        private static string ReadLog(GRYLog log)
        {
            return File.ReadAllText(log.Configuration.LogTargets.OfType<LogFile>().Single().File.GetPath(log.BasePath));
        }

        [TestMethod]
        [TestProperty(nameof(GRYLibrary.Core.Misc.TestKind), nameof(GRYLibrary.Core.Misc.TestKind.UnitTest))]
        public void ANotificationCancelsTheCallWhichItNames()
        {
            // arrange
            string folder = CreateFolder();
            try
            {
                using GRYLog log = CreateLog(folder);
                RunningToolCalls runningToolCalls = new RunningToolCalls();
                using RunningToolCall runningToolCall = runningToolCalls.Start(CreateIdentity(UrlNamespace, Client, 1));
                ToolCallCancellationHandler handler = new ToolCallCancellationHandler(runningToolCalls, log, UrlNamespace, Client);

                // act
                handler.HandleAsync(CreateNotification(@"{""requestId"":1,""reason"":""the client does not need the answer anymore""}"), CancellationToken.None).GetAwaiter().GetResult();

                // assert
                Assert.IsTrue(runningToolCall.WasCancelled);
                string writtenLog = ReadLog(log);
                Assert.IsTrue(writtenLog.Contains(Client, StringComparison.Ordinal) && writtenLog.Contains(UrlNamespace, StringComparison.Ordinal), $"A notification is answered with nothing, so the log is the only place at which the operator sees which call was stopped. It states: {writtenLog}");
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }

        /// <remarks>
        /// A notification which carries nothing names no call. It is no failure either: a notification is answered with
        /// nothing at all, so there is nobody who could be told about it.
        /// </remarks>
        [TestMethod]
        [TestProperty(nameof(GRYLibrary.Core.Misc.TestKind), nameof(GRYLibrary.Core.Misc.TestKind.UnitTest))]
        public void ANotificationWithoutParametersStopsNothing()
        {
            // arrange
            string folder = CreateFolder();
            try
            {
                using GRYLog log = CreateLog(folder);
                RunningToolCalls runningToolCalls = new RunningToolCalls();
                using RunningToolCall runningToolCall = runningToolCalls.Start(CreateIdentity(UrlNamespace, Client, 1));
                ToolCallCancellationHandler handler = new ToolCallCancellationHandler(runningToolCalls, log, UrlNamespace, Client);

                // act
                handler.HandleAsync(CreateNotification(null), CancellationToken.None).GetAwaiter().GetResult();

                // assert
                Assert.IsFalse(runningToolCall.WasCancelled);
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }

        /// <remarks>
        /// The notification states nothing but a request-id, and such an id is only unambiguous within the client which
        /// chose it. Which client is cancelling follows from the access-token of the request the notification arrived
        /// with, so a client can not even name the call of another one.
        /// </remarks>
        [TestMethod]
        [TestProperty(nameof(GRYLibrary.Core.Misc.TestKind), nameof(GRYLibrary.Core.Misc.TestKind.UnitTest))]
        public void ANotificationOfOneClientDoesNotStopTheCallOfAnother()
        {
            // arrange
            string folder = CreateFolder();
            try
            {
                using GRYLog log = CreateLog(folder);
                RunningToolCalls runningToolCalls = new RunningToolCalls();
                using RunningToolCall callOfTheOtherClient = runningToolCalls.Start(CreateIdentity(UrlNamespace, AnotherClient, 1));
                ToolCallCancellationHandler handler = new ToolCallCancellationHandler(runningToolCalls, log, UrlNamespace, Client);

                // act
                handler.HandleAsync(CreateNotification(@"{""requestId"":1,""reason"":""the client does not need the answer anymore""}"), CancellationToken.None).GetAwaiter().GetResult();

                // assert
                Assert.IsFalse(callOfTheOtherClient.WasCancelled);
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }

        /// <remarks>
        /// Which mcp-server is meant follows from the route the notification arrived at, so a cancellation which was
        /// sent to one mcp-server can not stop a call of another one - not even one of the same client.
        /// </remarks>
        [TestMethod]
        [TestProperty(nameof(GRYLibrary.Core.Misc.TestKind), nameof(GRYLibrary.Core.Misc.TestKind.UnitTest))]
        public void ANotificationAtOneMCPServerDoesNotStopACallAtAnother()
        {
            // arrange
            string folder = CreateFolder();
            try
            {
                using GRYLog log = CreateLog(folder);
                RunningToolCalls runningToolCalls = new RunningToolCalls();
                using RunningToolCall callAtTheOtherMCPServer = runningToolCalls.Start(CreateIdentity(AnotherUrlNamespace, Client, 1));
                ToolCallCancellationHandler handler = new ToolCallCancellationHandler(runningToolCalls, log, UrlNamespace, Client);

                // act
                handler.HandleAsync(CreateNotification(@"{""requestId"":1,""reason"":""the client does not need the answer anymore""}"), CancellationToken.None).GetAwaiter().GetResult();

                // assert
                Assert.IsFalse(callAtTheOtherMCPServer.WasCancelled);
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }
    }
}
