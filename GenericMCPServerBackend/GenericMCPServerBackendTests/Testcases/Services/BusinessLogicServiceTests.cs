using GenericMCPServerBackend.Core.Model;
using GenericMCPServerBackend.Core.Services;
using GenericMCPServerBackend.Tests.TestUtilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;

namespace GenericMCPServerBackend.Tests.Testcases.Services
{
    /// <remarks>
    /// Every testcase brings its own script and its own declaration in a temporary folder. That is deliberate: the
    /// codeunit knows no tool, so a testcase which used one of the example-tools of the repository would test the
    /// example instead of the mechanism.
    ///
    /// The virtual environments of the testcases live in one folder of the test-class: an environment belongs to a
    /// tool-name, so the testcases which use the same tool-name build it once instead of once per testcase.
    /// </remarks>
    [TestClass]
    public class BusinessLogicServiceTests
    {
        private const string UrlNamespace = MCPServerWrapper.UrlNamespace;

        /// <summary>A script which answers with the value it got, and fails if that value is "fail".</summary>
        private const string ScriptContent = @"import argparse
import sys

parser = argparse.ArgumentParser()
parser.add_argument('--value', required=True)
arguments = parser.parse_args()
if arguments.value == 'fail':
    sys.stderr.write('the value must not be fail')
    sys.exit(1)
sys.stdout.write(arguments.value)
";

        /// <summary>A script which answers with the interpreter it is running in.</summary>
        private const string InterpreterScriptContent = @"import sys

sys.stdout.write(sys.executable)
";

        /// <summary>A shell-script which answers with the value it got.</summary>
        private const string ShellScriptContent = "value=\"\"\nfor argument in \"$@\"; do\n  case \"$argument\" in\n    --value=*) value=\"${argument#--value=}\" ;;\n  esac\ndone\nprintf '%s' \"$value\"\n";

        private static readonly string _VirtualEnvironmentsFolder = CreateFolder();

        [ClassCleanup]
        public static void ClassCleanup()
        {
            if (Directory.Exists(_VirtualEnvironmentsFolder))
            {
                Directory.Delete(_VirtualEnvironmentsFolder, true);
            }
        }

        /// <summary>
        /// Creates a folder which contains the scripts and a tool-definitions-file with the given content, and returns
        /// the path of that file.
        /// </summary>
        private static string CreateToolDefinitions(string folder, string toolDefinitions)
        {
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, "Repeat.py"), ScriptContent);
            File.WriteAllText(Path.Combine(folder, "Interpreter.py"), InterpreterScriptContent);
            File.WriteAllText(Path.Combine(folder, "Repeat.sh"), ShellScriptContent);
            string file = Path.Combine(folder, "Tools.json");
            File.WriteAllText(file, MCPServerWrapper.WrapInAnMCPServer(toolDefinitions));
            return file;
        }

        private static string CreateFolder()
        {
            return Path.Combine(Path.GetTempPath(), "GenericMCPServerBackendTests", Guid.NewGuid().ToString());
        }

        private static IBusinessLogicService CreateBusinessLogicService(string folder, string toolDefinitions)
        {
            return new BusinessLogicService(CreateToolDefinitions(folder, toolDefinitions), _VirtualEnvironmentsFolder);
        }

        private const string OneTool = @"{
            ""tools"": [
                {
                    ""tool-name"": ""repeat"",
                    ""type"": ""python"",
                    ""calls"": ""Repeat.py"",
                    ""description"": ""Answers with the value it got."",
                    ""parameters"": [ ""value"" ]
                }
            ]
        }";

        [TestMethod]
        [TestProperty(nameof(GRYLibrary.Core.Misc.TestKind), nameof(GRYLibrary.Core.Misc.TestKind.UnitTest))]
        public void ADeclaredToolIsOfferedWithEverythingItWasDeclaredWith()
        {
            // arrange
            string folder = CreateFolder();
            try
            {
                string file = CreateToolDefinitions(folder, OneTool);

                // act
                IList<ToolDefinition> definitions = new BusinessLogicService(file, _VirtualEnvironmentsFolder).GetMCPServers()[0].Tools;

                // assert
                Assert.AreEqual(1, definitions.Count);
                Assert.AreEqual("repeat", definitions[0].ToolName);
                Assert.AreEqual(ToolTypes.Python, definitions[0].Type);
                Assert.AreEqual("Answers with the value it got.", definitions[0].Description);
                CollectionAssert.AreEqual(new string[] { "value" }, new List<string>(definitions[0].Parameters));
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }

        [TestMethod]
        [TestProperty(nameof(GRYLibrary.Core.Misc.TestKind), nameof(GRYLibrary.Core.Misc.TestKind.UnitTest))]
        public void RunningAToolAnswersWithWhatItsScriptWrote()
        {
            // arrange
            string folder = CreateFolder();
            try
            {
                IBusinessLogicService businessLogicService = CreateBusinessLogicService(folder, OneTool);

                // act
                string result = businessLogicService.RunTool(UrlNamespace, "repeat", new Dictionary<string, string>() { { "value", "hello" } }, CancellationToken.None);

                // assert
                Assert.AreEqual("hello", result);
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }

        /// <remarks>
        /// The environment of a tool is what makes the packages of two tools independent of each other, so a tool which
        /// ran in the interpreter of the system would silently share them with every other tool.
        /// </remarks>
        [TestMethod]
        [TestProperty(nameof(GRYLibrary.Core.Misc.TestKind), nameof(GRYLibrary.Core.Misc.TestKind.UnitTest))]
        public void APythonToolRunsInTheVirtualEnvironmentWhichBelongsToIt()
        {
            // arrange
            string folder = CreateFolder();
            try
            {
                IBusinessLogicService businessLogicService = CreateBusinessLogicService(folder, @"{
                    ""tools"": [
                        {
                            ""tool-name"": ""interpreter"",
                            ""type"": ""python"",
                            ""calls"": ""Interpreter.py"",
                            ""description"": ""Answers with the interpreter it runs in."",
                            ""parameters"": [ ]
                        }
                    ]
                }");

                // act
                string usedInterpreter = businessLogicService.RunTool(UrlNamespace, "interpreter", new Dictionary<string, string>(), CancellationToken.None);

                // assert
                StringAssert.StartsWith(usedInterpreter, Path.Combine(_VirtualEnvironmentsFolder, "interpreter"), "The tool has to run in the virtual environment which is named after it.");
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }

        /// <remarks>
        /// An environment which was built with other requirements must not be reused: otherwise an administrator would
        /// change the requirements of a tool and nothing would happen. The changed requirement here can not be
        /// installed, so the failure of the installation is what proves that the environment was built again.
        /// </remarks>
        [TestMethod]
        [TestProperty(nameof(GRYLibrary.Core.Misc.TestKind), nameof(GRYLibrary.Core.Misc.TestKind.UnitTest))]
        public void ChangedRequirementsMakeTheEnvironmentBeBuiltAgain()
        {
            // arrange
            string folder = CreateFolder();
            string toolWithoutRequirements = @"{
                ""tools"": [
                    {
                        ""tool-name"": ""repeat_with_requirements"",
                        ""type"": ""python"",
                        ""calls"": ""Repeat.py"",
                        ""description"": ""Answers with the value it got."",
                        ""parameters"": [ ""value"" ]
                    }
                ]
            }";
            string toolWithRequirements = @"{
                ""tools"": [
                    {
                        ""tool-name"": ""repeat_with_requirements"",
                        ""type"": ""python"",
                        ""calls"": ""Repeat.py"",
                        ""description"": ""Answers with the value it got."",
                        ""requirements"": [ ""this-package-does-not-exist-genericmcpserver"" ],
                        ""parameters"": [ ""value"" ]
                    }
                ]
            }";
            try
            {
                IBusinessLogicService withoutRequirements = CreateBusinessLogicService(folder, toolWithoutRequirements);
                Assert.AreEqual("hello", withoutRequirements.RunTool(UrlNamespace, "repeat_with_requirements", new Dictionary<string, string>() { { "value", "hello" } }, CancellationToken.None), "The environment has to exist before the requirements are changed.");
                IBusinessLogicService withRequirements = CreateBusinessLogicService(folder, toolWithRequirements);

                // act
                InvalidOperationException exception = Assert.ThrowsException<InvalidOperationException>(() => withRequirements.RunTool(UrlNamespace, "repeat_with_requirements", new Dictionary<string, string>() { { "value", "hello" } }, CancellationToken.None));

                // assert
                StringAssert.Contains(exception.Message, "repeat_with_requirements");
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }

        /// <remarks>
        /// A shell-tool is only supported on a linux-system, so on another system the reason has to be stated instead
        /// of a failure which looks like a broken script.
        /// </remarks>
        [TestMethod]
        [TestProperty(nameof(GRYLibrary.Core.Misc.TestKind), nameof(GRYLibrary.Core.Misc.TestKind.UnitTest))]
        public void AShellToolRunsOnLinuxAndIsRejectedElsewhere()
        {
            // arrange
            string folder = CreateFolder();
            try
            {
                IBusinessLogicService businessLogicService = CreateBusinessLogicService(folder, @"{
                    ""tools"": [
                        {
                            ""tool-name"": ""repeat_shell"",
                            ""type"": ""shell"",
                            ""calls"": ""Repeat.sh"",
                            ""description"": ""Answers with the value it got."",
                            ""parameters"": [ ""value"" ]
                        }
                    ]
                }");

                // act & assert
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    Assert.AreEqual("hello", businessLogicService.RunTool(UrlNamespace, "repeat_shell", new Dictionary<string, string>() { { "value", "hello" } }, CancellationToken.None));
                }
                else
                {
                    Assert.ThrowsException<PlatformNotSupportedException>(() => businessLogicService.RunTool(UrlNamespace, "repeat_shell", new Dictionary<string, string>() { { "value", "hello" } }, CancellationToken.None));
                }
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }

        /// <remarks>
        /// A value can not become a command: no shell is involved and every argument reaches the script as exactly one
        /// argument. The testcase uses a canary and no destructive payload - it asks the script whether it received
        /// the characters unchanged, because "the call failed" would only say that something went wrong while an
        /// unchanged value says that nothing of it was interpreted.
        /// </remarks>
        [TestMethod]
        [TestProperty(nameof(GRYLibrary.Core.Misc.TestKind), nameof(GRYLibrary.Core.Misc.TestKind.UnitTest))]
        public void AValueWhichContainsCharactersOfAShellStaysOneValue()
        {
            // arrange
            string folder = CreateFolder();
            try
            {
                IBusinessLogicService businessLogicService = CreateBusinessLogicService(folder, OneTool);
                string valueWhichLooksLikeACommand = "a; echo b && echo c | echo d > e $(echo f) `echo g` --help";

                // act
                string result = businessLogicService.RunTool(UrlNamespace, "repeat", new Dictionary<string, string>() { { "value", valueWhichLooksLikeACommand } }, CancellationToken.None);

                // assert
                Assert.AreEqual(valueWhichLooksLikeACommand, result, "The script has to receive the value unchanged and as one single argument.");
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }

        /// <remarks>
        /// A value which the script does not accept has to end up as a readable reason and not as a silent failure,
        /// because the client (and the model behind it) can only react to it if it learns what was wrong.
        /// </remarks>
        [TestMethod]
        [TestProperty(nameof(GRYLibrary.Core.Misc.TestKind), nameof(GRYLibrary.Core.Misc.TestKind.UnitTest))]
        public void AToolWhoseScriptFailsReportsTheReasonOfTheScript()
        {
            // arrange
            string folder = CreateFolder();
            try
            {
                IBusinessLogicService businessLogicService = CreateBusinessLogicService(folder, OneTool);

                // act
                InvalidOperationException exception = Assert.ThrowsException<InvalidOperationException>(() => businessLogicService.RunTool(UrlNamespace, "repeat", new Dictionary<string, string>() { { "value", "fail" } }, CancellationToken.None));

                // assert
                StringAssert.Contains(exception.Message, "the value must not be fail");
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }

        [TestMethod]
        [TestProperty(nameof(GRYLibrary.Core.Misc.TestKind), nameof(GRYLibrary.Core.Misc.TestKind.UnitTest))]
        public void AToolWhichIsNotDeclaredIsUnknown()
        {
            // arrange
            string folder = CreateFolder();
            try
            {
                IBusinessLogicService businessLogicService = CreateBusinessLogicService(folder, OneTool);

                // act & assert
                Assert.ThrowsException<KeyNotFoundException>(() => businessLogicService.RunTool(UrlNamespace, "something-else", new Dictionary<string, string>(), CancellationToken.None));
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }

        /// <remarks>
        /// Without a declaration the server offers nothing. This is the property which makes the codeunit generic: it
        /// brings no tool of its own along.
        /// </remarks>
        [TestMethod]
        [TestProperty(nameof(GRYLibrary.Core.Misc.TestKind), nameof(GRYLibrary.Core.Misc.TestKind.UnitTest))]
        public void WithoutAToolDefinitionsFileNoToolIsOffered()
        {
            Assert.AreEqual(0, new BusinessLogicService(null, _VirtualEnvironmentsFolder).GetMCPServers().Count);
        }

        [TestMethod]
        [TestProperty(nameof(GRYLibrary.Core.Misc.TestKind), nameof(GRYLibrary.Core.Misc.TestKind.UnitTest))]
        public void AToolDefinitionsFileWhichWasConfiguredButDoesNotExistIsAnError()
        {
            Assert.ThrowsException<FileNotFoundException>(() => new BusinessLogicService(Path.Combine(CreateFolder(), "Tools.json"), _VirtualEnvironmentsFolder));
        }

        /// <remarks>
        /// A mistake in the declaration has to be visible when the server starts and not only when somebody happens to
        /// call the affected tool.
        /// </remarks>
        [TestMethod]
        [TestProperty(nameof(GRYLibrary.Core.Misc.TestKind), nameof(GRYLibrary.Core.Misc.TestKind.UnitTest))]
        public void AnIncompleteDeclarationIsAnError()
        {
            // arrange
            string folder = CreateFolder();
            try
            {
                string withoutAScript = @"{ ""tools"": [ { ""tool-name"": ""repeat"", ""type"": ""python"", ""description"": ""x"" } ] }";
                string file = CreateToolDefinitions(folder, withoutAScript);

                // act & assert
                Assert.ThrowsException<InvalidOperationException>(() => new BusinessLogicService(file, _VirtualEnvironmentsFolder));
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }

        /// <remarks>
        /// Which interpreter runs a script decides what the script may do, so it is declared and not guessed from the
        /// name of the file.
        /// </remarks>
        [TestMethod]
        [TestProperty(nameof(GRYLibrary.Core.Misc.TestKind), nameof(GRYLibrary.Core.Misc.TestKind.UnitTest))]
        public void ADeclarationWithoutATypeIsAnError()
        {
            // arrange
            string folder = CreateFolder();
            try
            {
                string withoutAType = @"{ ""tools"": [ { ""tool-name"": ""repeat"", ""calls"": ""Repeat.py"", ""description"": ""x"" } ] }";
                string file = CreateToolDefinitions(folder, withoutAType);

                // act & assert
                Assert.ThrowsException<InvalidOperationException>(() => new BusinessLogicService(file, _VirtualEnvironmentsFolder));
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }

        [TestMethod]
        [TestProperty(nameof(GRYLibrary.Core.Misc.TestKind), nameof(GRYLibrary.Core.Misc.TestKind.UnitTest))]
        public void ADeclarationWithAnUnknownTypeIsAnError()
        {
            // arrange
            string folder = CreateFolder();
            try
            {
                string withAnUnknownType = @"{ ""tools"": [ { ""tool-name"": ""repeat"", ""type"": ""perl"", ""calls"": ""Repeat.py"", ""description"": ""x"" } ] }";
                string file = CreateToolDefinitions(folder, withAnUnknownType);

                // act
                InvalidOperationException exception = Assert.ThrowsException<InvalidOperationException>(() => new BusinessLogicService(file, _VirtualEnvironmentsFolder));

                // assert: the message has to state what is possible instead of only that the value is wrong
                StringAssert.Contains(exception.Message, ToolTypes.Python);
                StringAssert.Contains(exception.Message, ToolTypes.Shell);
                StringAssert.Contains(exception.Message, ToolTypes.WebRequest);
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }

        /// <remarks>
        /// Requirements belong to a python-tool alone. Ignoring them for another type would let an administrator
        /// believe in an effect which does not exist.
        /// </remarks>
        [TestMethod]
        [TestProperty(nameof(GRYLibrary.Core.Misc.TestKind), nameof(GRYLibrary.Core.Misc.TestKind.UnitTest))]
        public void RequirementsOfAToolWhichIsNoPythonToolAreAnError()
        {
            // arrange
            string folder = CreateFolder();
            try
            {
                string shellToolWithRequirements = @"{ ""tools"": [ { ""tool-name"": ""repeat_shell"", ""type"": ""shell"", ""calls"": ""Repeat.sh"", ""description"": ""x"", ""requirements"": [ ""requests"" ] } ] }";
                string file = CreateToolDefinitions(folder, shellToolWithRequirements);

                // act & assert
                Assert.ThrowsException<InvalidOperationException>(() => new BusinessLogicService(file, _VirtualEnvironmentsFolder));
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }
    }
}
