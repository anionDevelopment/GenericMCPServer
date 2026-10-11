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
    /// Covers the environment-variables a declaration hands to its script.
    /// </summary>
    /// <remarks>
    /// An environment-variable belongs to the operator of the server and is set the way the operator does it: as an
    /// environment-variable of the server-process (in a container, that is the compose-file). A script receives none of
    /// them by itself, so these testcases prove both that a declared one reaches the script and that an undeclared one
    /// does not. Every testcase uses its own names and removes them again, so that none of them depends on another.
    ///
    /// The virtual environments live in one folder of the test-class, because an environment belongs to a tool-name and
    /// would otherwise be built once per testcase.
    /// </remarks>
    [TestClass]
    public class EnvironmentVariablesTests
    {
        private const string UrlNamespace = MCPServerWrapper.UrlNamespace;

        /// <summary>A script which answers with the three variables this test is about.</summary>
        private const string ScriptContent = @"import os
import sys

sys.stdout.write(
    os.environ.get('GENERICMCPSERVER_TEST_MANDATORY', '<none>')
    + '|' + os.environ.get('GENERICMCPSERVER_TEST_OPTIONAL', '<none>')
    + '|' + os.environ.get('GENERICMCPSERVER_TEST_UNDECLARED', '<none>')
)
";

        private const string MandatoryVariable = "GENERICMCPSERVER_TEST_MANDATORY";

        private const string OptionalVariable = "GENERICMCPSERVER_TEST_OPTIONAL";

        private const string UndeclaredVariable = "GENERICMCPSERVER_TEST_UNDECLARED";

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

        private static string CreateToolDefinitions(string folder, string toolDefinitions)
        {
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, "EnvironmentVariables.py"), ScriptContent);
            string file = Path.Combine(folder, "Tools.json");
            File.WriteAllText(file, MCPServerWrapper.WrapInAnMCPServer(toolDefinitions));
            return file;
        }

        private static IBusinessLogicService CreateBusinessLogicService(string folder, string toolDefinitions)
        {
            return new BusinessLogicService(CreateToolDefinitions(folder, toolDefinitions), _VirtualEnvironmentsFolder);
        }

        /// <summary>The tool "env" declares the mandatory variable of this test.</summary>
        private const string ToolWithAMandatoryVariable = @"{
            ""tools"": [
                {
                    ""tool-name"": ""env"",
                    ""type"": ""python"",
                    ""calls"": ""EnvironmentVariables.py"",
                    ""description"": ""Answers with the environment-variables it can see."",
                    ""mandatory-env-variables"": [ ""GENERICMCPSERVER_TEST_MANDATORY"" ],
                    ""parameters"": [ ]
                }
            ]
        }";

        /// <summary>The tool "env" declares the optional variable of this test.</summary>
        private const string ToolWithAnOptionalVariable = @"{
            ""tools"": [
                {
                    ""tool-name"": ""env"",
                    ""type"": ""python"",
                    ""calls"": ""EnvironmentVariables.py"",
                    ""description"": ""Answers with the environment-variables it can see."",
                    ""optional-env-variables"": [ ""GENERICMCPSERVER_TEST_OPTIONAL"" ],
                    ""parameters"": [ ]
                }
            ]
        }";

        [TestMethod]
        [TestProperty(nameof(GRYLibrary.Core.Misc.TestKind), nameof(GRYLibrary.Core.Misc.TestKind.UnitTest))]
        public void AScriptReceivesAMandatoryEnvironmentVariable()
        {
            // arrange
            string folder = CreateFolder();
            Environment.SetEnvironmentVariable(MandatoryVariable, "the-value-of-the-mandatory-variable");
            try
            {
                IBusinessLogicService businessLogicService = CreateBusinessLogicService(folder, ToolWithAMandatoryVariable);

                // act
                string answer = businessLogicService.RunTool(UrlNamespace, "env", new Dictionary<string, string>(), CancellationToken.None);

                // assert
                Assert.AreEqual("the-value-of-the-mandatory-variable|<none>|<none>", answer);
            }
            finally
            {
                Environment.SetEnvironmentVariable(MandatoryVariable, null);
                Directory.Delete(folder, true);
            }
        }

        /// <remarks>
        /// A mandatory variable is what a script can not run without, so a missing one is a failure of the call and not
        /// a script which is run with a value which is not there.
        /// </remarks>
        [TestMethod]
        [TestProperty(nameof(GRYLibrary.Core.Misc.TestKind), nameof(GRYLibrary.Core.Misc.TestKind.UnitTest))]
        public void AMissingMandatoryEnvironmentVariableFailsTheCall()
        {
            // arrange
            string folder = CreateFolder();
            Environment.SetEnvironmentVariable(MandatoryVariable, null);
            try
            {
                IBusinessLogicService businessLogicService = CreateBusinessLogicService(folder, ToolWithAMandatoryVariable);

                // act & assert
                InvalidOperationException exception = Assert.ThrowsException<InvalidOperationException>(() => businessLogicService.RunTool(UrlNamespace, "env", new Dictionary<string, string>(), CancellationToken.None));
                StringAssert.Contains(exception.Message, MandatoryVariable, "The message has to name the variable which is missing.");
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }

        /// <remarks>
        /// "Set" is not enough: a variable which holds only whitespace carries no value the script could use, so it is
        /// treated like a missing one.
        /// </remarks>
        [TestMethod]
        [TestProperty(nameof(GRYLibrary.Core.Misc.TestKind), nameof(GRYLibrary.Core.Misc.TestKind.UnitTest))]
        public void AMandatoryEnvironmentVariableWhichHoldsOnlyWhitespaceFailsTheCall()
        {
            // arrange
            string folder = CreateFolder();
            Environment.SetEnvironmentVariable(MandatoryVariable, "   ");
            try
            {
                IBusinessLogicService businessLogicService = CreateBusinessLogicService(folder, ToolWithAMandatoryVariable);

                // act & assert
                InvalidOperationException exception = Assert.ThrowsException<InvalidOperationException>(() => businessLogicService.RunTool(UrlNamespace, "env", new Dictionary<string, string>(), CancellationToken.None));
                StringAssert.Contains(exception.Message, MandatoryVariable, "The message has to name the variable which carries no value.");
            }
            finally
            {
                Environment.SetEnvironmentVariable(MandatoryVariable, null);
                Directory.Delete(folder, true);
            }
        }

        [TestMethod]
        [TestProperty(nameof(GRYLibrary.Core.Misc.TestKind), nameof(GRYLibrary.Core.Misc.TestKind.UnitTest))]
        public void AnOptionalEnvironmentVariableIsHandedOverWhenItIsAvailable()
        {
            // arrange
            string folder = CreateFolder();
            Environment.SetEnvironmentVariable(OptionalVariable, "the-value-of-the-optional-variable");
            try
            {
                IBusinessLogicService businessLogicService = CreateBusinessLogicService(folder, ToolWithAnOptionalVariable);

                // act
                string answer = businessLogicService.RunTool(UrlNamespace, "env", new Dictionary<string, string>(), CancellationToken.None);

                // assert
                Assert.AreEqual("<none>|the-value-of-the-optional-variable|<none>", answer);
            }
            finally
            {
                Environment.SetEnvironmentVariable(OptionalVariable, null);
                Directory.Delete(folder, true);
            }
        }

        /// <remarks>
        /// The difference to a mandatory variable: a missing optional one is left out and the script runs all the same.
        /// </remarks>
        [TestMethod]
        [TestProperty(nameof(GRYLibrary.Core.Misc.TestKind), nameof(GRYLibrary.Core.Misc.TestKind.UnitTest))]
        public void AMissingOptionalEnvironmentVariableDoesNotKeepTheScriptFromRunning()
        {
            // arrange
            string folder = CreateFolder();
            Environment.SetEnvironmentVariable(OptionalVariable, null);
            try
            {
                IBusinessLogicService businessLogicService = CreateBusinessLogicService(folder, ToolWithAnOptionalVariable);

                // act
                string answer = businessLogicService.RunTool(UrlNamespace, "env", new Dictionary<string, string>(), CancellationToken.None);

                // assert
                Assert.AreEqual("<none>|<none>|<none>", answer, "The script has to run even though the optional variable is not available.");
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }

        /// <remarks>
        /// This is the point of the feature: a variable which an operator sets for the server (the admin-token, a
        /// database-connection, ...) must not reach a tool which did not declare it, although the server-process can
        /// see it.
        /// </remarks>
        [TestMethod]
        [TestProperty(nameof(GRYLibrary.Core.Misc.TestKind), nameof(GRYLibrary.Core.Misc.TestKind.UnitTest))]
        public void AScriptDoesNotReceiveAnEnvironmentVariableOfTheServerWhichItDidNotDeclare()
        {
            // arrange
            string folder = CreateFolder();
            Environment.SetEnvironmentVariable(MandatoryVariable, "the-value-of-the-mandatory-variable");
            Environment.SetEnvironmentVariable(UndeclaredVariable, "a-secret-of-the-server-which-no-tool-declared");
            try
            {
                IBusinessLogicService businessLogicService = CreateBusinessLogicService(folder, ToolWithAMandatoryVariable);

                // act
                string answer = businessLogicService.RunTool(UrlNamespace, "env", new Dictionary<string, string>(), CancellationToken.None);

                // assert
                Assert.AreEqual("the-value-of-the-mandatory-variable|<none>|<none>", answer, "The variable which the tool did not declare must not be visible to its script.");
            }
            finally
            {
                Environment.SetEnvironmentVariable(MandatoryVariable, null);
                Environment.SetEnvironmentVariable(UndeclaredVariable, null);
                Directory.Delete(folder, true);
            }
        }

        /// <remarks>
        /// The feature is for the two types which run a script. A shell-declaration therefore accepts
        /// environment-variables; this is checked when the declarations are read, so it is independent of the system
        /// the test runs on.
        /// </remarks>
        [TestMethod]
        [TestProperty(nameof(GRYLibrary.Core.Misc.TestKind), nameof(GRYLibrary.Core.Misc.TestKind.UnitTest))]
        public void AShellDeclarationMayDeclareEnvironmentVariables()
        {
            // arrange
            string folder = CreateFolder();
            try
            {
                string shellToolWithEnvironmentVariables = @"{ ""tools"": [ { ""tool-name"": ""shell_env"", ""type"": ""shell"", ""calls"": ""GetTime.sh"", ""description"": ""x"", ""mandatory-env-variables"": [ ""GENERICMCPSERVER_TEST_MANDATORY"" ], ""optional-env-variables"": [ ""GENERICMCPSERVER_TEST_OPTIONAL"" ] } ] }";

                // act & assert
                _ = CreateBusinessLogicService(folder, shellToolWithEnvironmentVariables);
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }

        /// <remarks>
        /// A web-request runs no script, so there is nothing an environment-variable could reach. Stating one is a
        /// misunderstanding and therefore a mistake of the declaration instead of something which is silently ignored.
        /// </remarks>
        [TestMethod]
        [TestProperty(nameof(GRYLibrary.Core.Misc.TestKind), nameof(GRYLibrary.Core.Misc.TestKind.UnitTest))]
        public void AWebRequestToolWhichDeclaresEnvironmentVariablesIsAnError()
        {
            // arrange
            string folder = CreateFolder();
            try
            {
                string webRequestToolWithEnvironmentVariables = @"{ ""tools"": [ { ""tool-name"": ""page"", ""type"": ""web-request"", ""description"": ""x"", ""allowed-urls"": [ ""example\\.com/.*"" ], ""mandatory-env-variables"": [ ""GENERICMCPSERVER_TEST_MANDATORY"" ] } ] }";

                // act & assert
                Assert.ThrowsException<InvalidOperationException>(() => CreateBusinessLogicService(folder, webRequestToolWithEnvironmentVariables));
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }

        /// <remarks>
        /// A variable can not be mandatory and optional at once: that would be two contradicting statements about
        /// whether the script may run without it.
        /// </remarks>
        [TestMethod]
        [TestProperty(nameof(GRYLibrary.Core.Misc.TestKind), nameof(GRYLibrary.Core.Misc.TestKind.UnitTest))]
        public void AnEnvironmentVariableStatedAsMandatoryAndOptionalIsAnError()
        {
            // arrange
            string folder = CreateFolder();
            try
            {
                string toolWithAContradiction = @"{ ""tools"": [ { ""tool-name"": ""env"", ""type"": ""python"", ""calls"": ""EnvironmentVariables.py"", ""description"": ""x"", ""mandatory-env-variables"": [ ""GENERICMCPSERVER_TEST_MANDATORY"" ], ""optional-env-variables"": [ ""GENERICMCPSERVER_TEST_MANDATORY"" ] } ] }";

                // act & assert
                Assert.ThrowsException<InvalidOperationException>(() => CreateBusinessLogicService(folder, toolWithAContradiction));
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }
    }
}
