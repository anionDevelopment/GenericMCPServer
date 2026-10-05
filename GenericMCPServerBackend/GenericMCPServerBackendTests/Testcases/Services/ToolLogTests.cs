using GenericMCPServerBackend.Core.Services;
using GenericMCPServerBackend.Tests.TestUtilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace GenericMCPServerBackend.Tests.Testcases.Services
{
    /// <summary>
    /// Covers the log-file which every tool-call writes: the exact invocation and the output of a run are appended to
    /// "&lt;tool-name&gt;.log" in the log-folder, so that an operator can see afterwards what a tool was called with and
    /// what it answered.
    /// </summary>
    [TestClass]
    public class ToolLogTests
    {
        private const string UrlNamespace = MCPServerWrapper.UrlNamespace;

        /// <summary>A script which answers with the value of its parameter, so that the output is something known.</summary>
        private const string ScriptContent = @"import argparse
import sys

parser = argparse.ArgumentParser()
parser.add_argument('--message', required=True)
arguments = parser.parse_args()
sys.stdout.write('echo:' + arguments.message)
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

        private static string CreateFolder()
        {
            return Path.Combine(Path.GetTempPath(), "GenericMCPServerBackendTests", Guid.NewGuid().ToString());
        }

        private const string ToolWhichEchoes = @"{
            ""tools"": [
                {
                    ""tool-name"": ""echo"",
                    ""type"": ""python"",
                    ""calls"": ""Echo.py"",
                    ""description"": ""Answers with the value of its parameter."",
                    ""parameters"": [ ""message"" ]
                }
            ]
        }";

        private static IBusinessLogicService CreateBusinessLogicService(string folder, string logFolder)
        {
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, "Echo.py"), ScriptContent);
            string file = Path.Combine(folder, "Tools.json");
            File.WriteAllText(file, MCPServerWrapper.WrapInAnMCPServer(ToolWhichEchoes));
            return new BusinessLogicService(file, _VirtualEnvironmentsFolder, logFolder);
        }

        [TestMethod]
        [TestProperty(nameof(GRYLibrary.Core.Misc.TestKind), nameof(GRYLibrary.Core.Misc.TestKind.UnitTest))]
        public void AToolCallIsLoggedWithItsInvocationAndItsOutput()
        {
            // arrange
            string folder = CreateFolder();
            string logFolder = CreateFolder();
            try
            {
                IBusinessLogicService businessLogicService = CreateBusinessLogicService(folder, logFolder);

                // act
                string answer = businessLogicService.RunTool(UrlNamespace, "echo", new Dictionary<string, string>() { { "message", "hello-canary" } }, CancellationToken.None);

                // assert
                Assert.AreEqual("echo:hello-canary", answer);
                string logFile = Path.Combine(logFolder, "echo.log");
                Assert.IsTrue(File.Exists(logFile), "The tool-call has to be logged into \"<tool-name>.log\".");
                string log = File.ReadAllText(logFile);
                StringAssert.Contains(log, "--message=hello-canary", "The exact invocation has to be part of the log.");
                StringAssert.Contains(log, "echo:hello-canary", "The output of the tool has to be part of the log.");
            }
            finally
            {
                Directory.Delete(folder, true);
                Directory.Delete(logFolder, true);
            }
        }

        /// <remarks>
        /// Two calls of the same tool append to the same file instead of replacing it, so that the log is a history of
        /// the calls and not only the last one.
        /// </remarks>
        [TestMethod]
        [TestProperty(nameof(GRYLibrary.Core.Misc.TestKind), nameof(GRYLibrary.Core.Misc.TestKind.UnitTest))]
        public void ASecondCallIsAppendedToTheLog()
        {
            // arrange
            string folder = CreateFolder();
            string logFolder = CreateFolder();
            try
            {
                IBusinessLogicService businessLogicService = CreateBusinessLogicService(folder, logFolder);

                // act
                businessLogicService.RunTool(UrlNamespace, "echo", new Dictionary<string, string>() { { "message", "first" } }, CancellationToken.None);
                businessLogicService.RunTool(UrlNamespace, "echo", new Dictionary<string, string>() { { "message", "second" } }, CancellationToken.None);

                // assert
                string log = File.ReadAllText(Path.Combine(logFolder, "echo.log"));
                StringAssert.Contains(log, "echo:first");
                StringAssert.Contains(log, "echo:second", "The second call must not replace the first one in the log.");
            }
            finally
            {
                Directory.Delete(folder, true);
                Directory.Delete(logFolder, true);
            }
        }

        /// <remarks>
        /// The log is optional: a server which was given no log-folder runs its tools exactly as before and writes
        /// nothing.
        /// </remarks>
        [TestMethod]
        [TestProperty(nameof(GRYLibrary.Core.Misc.TestKind), nameof(GRYLibrary.Core.Misc.TestKind.UnitTest))]
        public void AToolRunsWithoutALogFolder()
        {
            // arrange
            string folder = CreateFolder();
            try
            {
                IBusinessLogicService businessLogicService = CreateBusinessLogicService(folder, null);

                // act
                string answer = businessLogicService.RunTool(UrlNamespace, "echo", new Dictionary<string, string>() { { "message", "no-log" } }, CancellationToken.None);

                // assert
                Assert.AreEqual("echo:no-log", answer);
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }
    }
}
