using GenericMCPServerBackend.Core.Services;
using GenericMCPServerBackend.Tests.TestUtilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace GenericMCPServerBackend.Tests.Testcases.Services
{
    /// <summary>
    /// Covers what the cancellation of a tool-call does to the program behind that call: it really stops it instead of
    /// only letting nobody wait for it anymore.
    /// </summary>
    /// <remarks>
    /// Every testcase uses the canary of <see cref="WaitingScript"/> and not the answer of the call to state that,
    /// because the answer says nothing about whether the program is gone.
    /// </remarks>
    [TestClass]
    public class CancellationTests
    {
        private const string UrlNamespace = MCPServerWrapper.UrlNamespace;

        /// <summary>
        /// A script which starts another program and waits for it. A script of a tool is exactly the kind of program
        /// which starts another one, so this is what shows whether the whole tree is stopped or only the script which
        /// was started here. It hands its own value to that program, so the program which waits is the one below.
        /// </summary>
        private const string ScriptWhichStartsAnotherProgramContent = @"import argparse
import os
import subprocess
import sys

parser = argparse.ArgumentParser()
parser.add_argument('--value', required=True)
arguments = parser.parse_args()
child = subprocess.Popen([sys.executable, os.path.join(os.path.dirname(os.path.abspath(__file__)), '" + WaitingScript.FileName + @"'), '--value=' + arguments.value])
child.wait()
";

        private static readonly string _VirtualEnvironmentsFolder = CreateFolder();

        [ClassCleanup]
        public static void ClassCleanup()
        {
            if (Directory.Exists(_VirtualEnvironmentsFolder))
            {
                Directory.Delete(_VirtualEnvironmentsFolder, true);
            }
        }

        private const string TheTools = @"{
            ""tools"": [
                {
                    ""tool-name"": ""waiting"",
                    ""type"": ""python"",
                    ""calls"": """ + WaitingScript.FileName + @""",
                    ""description"": ""Waits until it is released."",
                    ""parameters"": [ ""value"" ]
                },
                {
                    ""tool-name"": ""waiting_in_another_program"",
                    ""type"": ""python"",
                    ""calls"": ""StartAnotherProgram.py"",
                    ""description"": ""Starts a program which waits until it is released."",
                    ""parameters"": [ ""value"" ]
                },
                {
                    ""tool-name"": ""example_page"",
                    ""type"": ""web-request"",
                    ""description"": ""Requests a page of an address which no name-service resolves."",
                    ""allowed-urls"": [ ""example\\.invalid/.*"" ]
                }
            ]
        }";

        private static string CreateFolder()
        {
            return Path.Combine(Path.GetTempPath(), "GenericMCPServerBackendTests", Guid.NewGuid().ToString());
        }

        private static IBusinessLogicService CreateBusinessLogicService(string folder)
        {
            Directory.CreateDirectory(folder);
            WaitingScript.WriteInto(folder);
            File.WriteAllText(Path.Combine(folder, "StartAnotherProgram.py"), ScriptWhichStartsAnotherProgramContent);
            string file = Path.Combine(folder, "Tools.json");
            File.WriteAllText(file, MCPServerWrapper.WrapInAnMCPServer(TheTools));
            return new BusinessLogicService(file, _VirtualEnvironmentsFolder);
        }

        /// <summary>
        /// Runs the given action and returns the exception it ended with, or null when it ended without one.
        /// </summary>
        private static Exception? GetExceptionOf(Action action)
        {
            try
            {
                action();
                return null;
            }
            catch (Exception exception)
            {
                return exception;
            }
        }

        /// <remarks>
        /// This is what the feature is about: a client which cancels its call does not only stop waiting for an answer,
        /// the program which that call started is ended as well.
        /// </remarks>
        [TestMethod]
        [TestProperty(nameof(GRYLibrary.Core.Misc.TestKind), nameof(GRYLibrary.Core.Misc.TestKind.UnitTest))]
        public void TheScriptOfACancelledCallIsStopped()
        {
            // arrange
            string folder = CreateFolder();
            using WaitingScript script = new WaitingScript();
            try
            {
                IBusinessLogicService businessLogicService = CreateBusinessLogicService(folder);
                using CancellationTokenSource cancellation = new CancellationTokenSource();
                Task<string> call = Task.Run(() => businessLogicService.RunTool(UrlNamespace, "waiting", new Dictionary<string, string>() { { "value", script.ReportFolder } }, cancellation.Token));
                script.WaitUntilItRuns();

                // act
                cancellation.Cancel();

                // assert
                Assert.IsInstanceOfType(GetExceptionOf(() => call.GetAwaiter().GetResult()), typeof(OperationCanceledException), "A cancelled call ends as a cancellation and not as a failure of the tool.");
                Assert.IsFalse(script.ItStillRunsToItsEndAfterItWasReleased(), "The script has to be gone: a script which is still running writes its canary as soon as it is released.");
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }

        /// <remarks>
        /// A script which started another program would otherwise leave that one behind, and a script of a tool is
        /// exactly the kind of program which starts another one.
        /// </remarks>
        [TestMethod]
        [TestProperty(nameof(GRYLibrary.Core.Misc.TestKind), nameof(GRYLibrary.Core.Misc.TestKind.UnitTest))]
        public void AProgramWhichTheScriptOfACancelledCallStartedIsStoppedAsWell()
        {
            // arrange
            string folder = CreateFolder();
            using WaitingScript script = new WaitingScript();
            try
            {
                IBusinessLogicService businessLogicService = CreateBusinessLogicService(folder);
                using CancellationTokenSource cancellation = new CancellationTokenSource();
                Task<string> call = Task.Run(() => businessLogicService.RunTool(UrlNamespace, "waiting_in_another_program", new Dictionary<string, string>() { { "value", script.ReportFolder } }, cancellation.Token));
                script.WaitUntilItRuns();

                // act
                cancellation.Cancel();

                // assert
                Assert.IsInstanceOfType(GetExceptionOf(() => call.GetAwaiter().GetResult()), typeof(OperationCanceledException));
                Assert.IsFalse(script.ItStillRunsToItsEndAfterItWasReleased(), "The program which the script of the tool started has to be gone as well.");
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }

        /// <remarks>
        /// A cancellation can arrive before the script was started at all, because the call and the cancellation are two
        /// http-requests. Nothing is started then: the client already stated that it does not want the answer.
        /// </remarks>
        [TestMethod]
        [TestProperty(nameof(GRYLibrary.Core.Misc.TestKind), nameof(GRYLibrary.Core.Misc.TestKind.UnitTest))]
        public void ACallWhichWasCancelledBeforeItRanStartsNoScriptAtAll()
        {
            // arrange
            string folder = CreateFolder();
            using WaitingScript script = new WaitingScript();
            try
            {
                IBusinessLogicService businessLogicService = CreateBusinessLogicService(folder);
                using CancellationTokenSource cancellation = new CancellationTokenSource();
                cancellation.Cancel();

                // act
                Exception? exception = GetExceptionOf(() => businessLogicService.RunTool(UrlNamespace, "waiting", new Dictionary<string, string>() { { "value", script.ReportFolder } }, cancellation.Token));

                // assert
                Assert.IsInstanceOfType(exception, typeof(OperationCanceledException));
                Assert.IsFalse(script.ItStarted(), "The script must not have been started at all.");
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }

        /// <remarks>
        /// A tool of the type web-request runs no script, so the cancellation has to reach the request itself. The
        /// address of this testcase is one which no name-service resolves, so a request which was sent at all would end
        /// as a failed request and not as a cancellation - which is what tells the two apart here.
        /// </remarks>
        [TestMethod]
        [TestProperty(nameof(GRYLibrary.Core.Misc.TestKind), nameof(GRYLibrary.Core.Misc.TestKind.UnitTest))]
        public void AWebRequestOfACancelledCallIsNotSent()
        {
            // arrange
            string folder = CreateFolder();
            try
            {
                IBusinessLogicService businessLogicService = CreateBusinessLogicService(folder);
                using CancellationTokenSource cancellation = new CancellationTokenSource();
                cancellation.Cancel();

                // act
                Exception? exception = GetExceptionOf(() => businessLogicService.RunTool(UrlNamespace, "example_page", new Dictionary<string, string>() { { WebRequestService.UrlParameterName, "example.invalid/page" } }, cancellation.Token));

                // assert
                Assert.IsInstanceOfType(exception, typeof(OperationCanceledException));
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }
    }
}
