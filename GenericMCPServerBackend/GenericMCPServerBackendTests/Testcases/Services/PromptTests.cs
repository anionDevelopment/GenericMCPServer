using GenericMCPServerBackend.Core.Model;
using GenericMCPServerBackend.Core.Services;
using GenericMCPServerBackend.Tests.TestUtilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;

namespace GenericMCPServerBackend.Tests.Testcases.Services
{
    /// <summary>
    /// Covers the prompts: a text which a client asks for by its name and hands to its model, declared by an
    /// administrator exactly like a tool.
    /// </summary>
    /// <remarks>
    /// Every testcase brings its own script and its own declaration in a temporary folder, for the same reason as the
    /// testcases of the tools and of the resources do: the codeunit knows no prompt, so a testcase which used one of
    /// the examples of the repository would test the example instead of the mechanism.
    /// </remarks>
    [TestClass]
    public class PromptTests
    {
        private const string UrlNamespace = MCPServerWrapper.UrlNamespace;

        private const string NameOfThePrompt = "explain_topic";

        /// <summary>A script which writes a text into which the value it got is filled.</summary>
        private const string ScriptContent = @"import argparse
import sys

parser = argparse.ArgumentParser()
parser.add_argument('--topic', required=True)
arguments = parser.parse_args()
sys.stdout.write('Explain ' + arguments.topic + ' to a beginner.')
";

        /// <summary>A script which fails with a reason, so that a testcase can see what reaches the caller.</summary>
        private const string FailingScriptContent = @"import sys

sys.stderr.write('the prompt is not available')
sys.exit(1)
";

        /// <summary>A script which answers with the interpreter it is running in.</summary>
        private const string InterpreterScriptContent = @"import sys

sys.stdout.write(sys.executable)
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

        private const string OnePrompt = @"{
            ""prompts"": [
                {
                    ""prompt-name"": """ + NameOfThePrompt + @""",
                    ""type"": ""python"",
                    ""calls"": ""BuildPrompt.py"",
                    ""description"": ""Asks for an explanation of a topic."",
                    ""parameters"": [ ""topic"" ]
                }
            ]
        }";

        private static string CreateFolder()
        {
            return Path.Combine(Path.GetTempPath(), "GenericMCPServerBackendTests", Guid.NewGuid().ToString());
        }

        /// <summary>
        /// Creates a folder which contains the scripts and a tool-definitions-file with the given content, and returns
        /// the path of that file.
        /// </summary>
        private static string CreateDefinitions(string folder, string definitions)
        {
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, "BuildPrompt.py"), ScriptContent);
            File.WriteAllText(Path.Combine(folder, "FailingPrompt.py"), FailingScriptContent);
            File.WriteAllText(Path.Combine(folder, "Interpreter.py"), InterpreterScriptContent);
            string file = Path.Combine(folder, "Tools.json");
            File.WriteAllText(file, MCPServerWrapper.WrapInAnMCPServer(definitions));
            return file;
        }

        private static IBusinessLogicService CreateBusinessLogicService(string folder, string definitions)
        {
            return new BusinessLogicService(CreateDefinitions(folder, definitions), _VirtualEnvironmentsFolder);
        }

        [TestMethod]
        [TestProperty(nameof(GRYLibrary.Core.Misc.TestKind), nameof(GRYLibrary.Core.Misc.TestKind.UnitTest))]
        public void ADeclaredPromptIsOfferedWithEverythingItWasDeclaredWith()
        {
            // arrange
            string folder = CreateFolder();
            try
            {
                // act
                IList<PromptDefinition> definitions = CreateBusinessLogicService(folder, OnePrompt).GetMCPServers()[0].Prompts;

                // assert
                Assert.AreEqual(1, definitions.Count);
                Assert.AreEqual(NameOfThePrompt, definitions[0].PromptName);
                Assert.AreEqual("Asks for an explanation of a topic.", definitions[0].Description);
                Assert.AreEqual(ToolTypes.Python, definitions[0].Type);
                Assert.AreEqual(1, definitions[0].Parameters.Count);
                Assert.AreEqual("topic", definitions[0].Parameters[0]);
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }

        /// <remarks>
        /// The value of an argument comes from the client and is handed to the script exactly like the value of a
        /// parameter of a tool, which is what lets the script decide what the text of the prompt says.
        /// </remarks>
        [TestMethod]
        [TestProperty(nameof(GRYLibrary.Core.Misc.TestKind), nameof(GRYLibrary.Core.Misc.TestKind.UnitTest))]
        public void AskingForAPromptAnswersWithTheTextItsScriptWroteForTheGivenArguments()
        {
            // arrange
            string folder = CreateFolder();
            try
            {
                IBusinessLogicService businessLogicService = CreateBusinessLogicService(folder, OnePrompt);

                // act
                string text = businessLogicService.GetPrompt(UrlNamespace, NameOfThePrompt, new Dictionary<string, string>() { { "topic", "cryptography" } });

                // assert
                Assert.AreEqual("Explain cryptography to a beginner.", text);
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }

        [TestMethod]
        [TestProperty(nameof(GRYLibrary.Core.Misc.TestKind), nameof(GRYLibrary.Core.Misc.TestKind.UnitTest))]
        public void APromptWhichIsNotDeclaredIsUnknown()
        {
            // arrange
            string folder = CreateFolder();
            try
            {
                IBusinessLogicService businessLogicService = CreateBusinessLogicService(folder, OnePrompt);

                // act & assert
                Assert.ThrowsException<KeyNotFoundException>(() => businessLogicService.GetPrompt(UrlNamespace, "another_prompt", new Dictionary<string, string>()));
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }

        /// <remarks>
        /// What a script reported is what a caller needs to learn why it did not receive the text of the prompt.
        /// </remarks>
        [TestMethod]
        [TestProperty(nameof(GRYLibrary.Core.Misc.TestKind), nameof(GRYLibrary.Core.Misc.TestKind.UnitTest))]
        public void APromptWhoseScriptFailsReportsTheReasonOfTheScript()
        {
            // arrange
            string folder = CreateFolder();
            try
            {
                string failingPrompt = @"{
                    ""prompts"": [
                        {
                            ""prompt-name"": ""failing_prompt"",
                            ""type"": ""python"",
                            ""calls"": ""FailingPrompt.py"",
                            ""description"": ""A prompt which can not be built.""
                        }
                    ]
                }";
                IBusinessLogicService businessLogicService = CreateBusinessLogicService(folder, failingPrompt);

                // act
                InvalidOperationException exception = Assert.ThrowsException<InvalidOperationException>(() => businessLogicService.GetPrompt(UrlNamespace, "failing_prompt", new Dictionary<string, string>()));

                // assert
                StringAssert.Contains(exception.Message, "the prompt is not available");
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }

        /// <remarks>
        /// The environments of all declarations of a server lie in one folder, so a prompt and a tool of the same name
        /// would otherwise share one environment and rebuild it for each other as soon as their requirements differ.
        /// </remarks>
        [TestMethod]
        [TestProperty(nameof(GRYLibrary.Core.Misc.TestKind), nameof(GRYLibrary.Core.Misc.TestKind.UnitTest))]
        public void APromptAndAToolOfTheSameNameUseTwoVirtualEnvironments()
        {
            // arrange
            string folder = CreateFolder();
            try
            {
                string sameName = @"{
                    ""tools"": [
                        {
                            ""tool-name"": ""interpreter"",
                            ""type"": ""python"",
                            ""calls"": ""Interpreter.py"",
                            ""description"": ""Answers with its interpreter."",
                            ""parameters"": [ ]
                        }
                    ],
                    ""prompts"": [
                        {
                            ""prompt-name"": ""interpreter"",
                            ""type"": ""python"",
                            ""calls"": ""Interpreter.py"",
                            ""description"": ""Answers with its interpreter.""
                        }
                    ]
                }";
                IBusinessLogicService businessLogicService = CreateBusinessLogicService(folder, sameName);

                // act
                string interpreterOfTheTool = businessLogicService.RunTool(UrlNamespace, "interpreter", new Dictionary<string, string>());
                string interpreterOfThePrompt = businessLogicService.GetPrompt(UrlNamespace, "interpreter", new Dictionary<string, string>());

                // assert
                Assert.AreNotEqual(interpreterOfTheTool, interpreterOfThePrompt, "A prompt and a tool of the same name must not share one virtual environment.");
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }

        /// <remarks>
        /// A prompt is asked for by its name, so two prompts of the same name would make it undecidable which of them a
        /// client means.
        /// </remarks>
        [TestMethod]
        [TestProperty(nameof(GRYLibrary.Core.Misc.TestKind), nameof(GRYLibrary.Core.Misc.TestKind.UnitTest))]
        public void APromptNameWhichIsDeclaredTwiceInOneMCPServerIsAnError()
        {
            // arrange
            string folder = CreateFolder();
            try
            {
                string theSameNameTwice = @"{
                    ""prompts"": [
                        {
                            ""prompt-name"": """ + NameOfThePrompt + @""",
                            ""type"": ""python"",
                            ""calls"": ""BuildPrompt.py"",
                            ""description"": ""x""
                        },
                        {
                            ""prompt-name"": """ + NameOfThePrompt + @""",
                            ""type"": ""python"",
                            ""calls"": ""BuildPrompt.py"",
                            ""description"": ""x""
                        }
                    ]
                }";
                string file = CreateDefinitions(folder, theSameNameTwice);

                // act & assert
                Assert.ThrowsException<InvalidOperationException>(() => new BusinessLogicService(file, _VirtualEnvironmentsFolder));
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }

        /// <remarks>
        /// A declaration which is incomplete has to be visible when the server starts and not when a client happens to
        /// ask for the affected prompt.
        /// </remarks>
        [TestMethod]
        [TestProperty(nameof(GRYLibrary.Core.Misc.TestKind), nameof(GRYLibrary.Core.Misc.TestKind.UnitTest))]
        public void AnIncompleteDeclarationOfAPromptIsAnError()
        {
            // arrange
            string folder = CreateFolder();
            try
            {
                string withoutAName = @"{ ""prompts"": [ { ""type"": ""python"", ""calls"": ""BuildPrompt.py"", ""description"": ""x"" } ] }";
                string withoutAType = @"{ ""prompts"": [ { ""prompt-name"": """ + NameOfThePrompt + @""", ""calls"": ""BuildPrompt.py"", ""description"": ""x"" } ] }";
                string withoutAScript = @"{ ""prompts"": [ { ""prompt-name"": """ + NameOfThePrompt + @""", ""type"": ""python"", ""description"": ""x"" } ] }";
                string withAnArgumentWithoutAName = @"{ ""prompts"": [ { ""prompt-name"": """ + NameOfThePrompt + @""", ""type"": ""python"", ""calls"": ""BuildPrompt.py"", ""description"": ""x"", ""parameters"": [ """" ] } ] }";

                // act & assert
                foreach (string declaration in new string[] { withoutAName, withoutAType, withoutAScript, withAnArgumentWithoutAName })
                {
                    string file = CreateDefinitions(folder, declaration);
                    Assert.ThrowsException<InvalidOperationException>(() => new BusinessLogicService(file, _VirtualEnvironmentsFolder), $"The declaration \"{declaration}\" has to be rejected.");
                }
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }

        /// <remarks>
        /// A declaration of the type "web-request" takes the address it requests either from the client (a tool) or
        /// from its own uri (a resource), and a prompt has neither. Offering that type for a prompt would therefore
        /// mean inventing one of the two for it, so it is rejected instead of behaving like one of them without saying
        /// which.
        /// </remarks>
        [TestMethod]
        [TestProperty(nameof(GRYLibrary.Core.Misc.TestKind), nameof(GRYLibrary.Core.Misc.TestKind.UnitTest))]
        public void AWebRequestPromptIsAnError()
        {
            // arrange
            string folder = CreateFolder();
            try
            {
                string asAWebRequest = @"{ ""prompts"": [ { ""prompt-name"": """ + NameOfThePrompt + @""", ""type"": ""web-request"", ""description"": ""x"", ""allowed-urls"": [ ""example\\.com/.*"" ] } ] }";
                string file = CreateDefinitions(folder, asAWebRequest);

                // act
                InvalidOperationException exception = Assert.ThrowsException<InvalidOperationException>(() => new BusinessLogicService(file, _VirtualEnvironmentsFolder));

                // assert
                StringAssert.Contains(exception.Message, ToolTypes.WebRequest);
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }

        /// <remarks>
        /// Such a value belongs to a declaration which requests an address, which a prompt never is. Ignoring it would
        /// let an administrator believe in an effect which does not exist.
        /// </remarks>
        [TestMethod]
        [TestProperty(nameof(GRYLibrary.Core.Misc.TestKind), nameof(GRYLibrary.Core.Misc.TestKind.UnitTest))]
        public void HeadersOfAPromptAreAnError()
        {
            // arrange
            string folder = CreateFolder();
            try
            {
                string withHeaders = @"{ ""prompts"": [ { ""prompt-name"": """ + NameOfThePrompt + @""", ""type"": ""python"", ""calls"": ""BuildPrompt.py"", ""description"": ""x"", ""headers"": { ""Accept"": ""text/plain"" } } ] }";
                string file = CreateDefinitions(folder, withHeaders);

                // act & assert
                Assert.ThrowsException<InvalidOperationException>(() => new BusinessLogicService(file, _VirtualEnvironmentsFolder));
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }

        /// <remarks>
        /// A prompt-name is what a client asks for, and it is also used as a part of the name of the folder of the
        /// virtual environment of a python-prompt, so a name which contains a path-separator is rejected here instead
        /// of turning into a folder somewhere else.
        /// </remarks>
        [TestMethod]
        [TestProperty(nameof(GRYLibrary.Core.Misc.TestKind), nameof(GRYLibrary.Core.Misc.TestKind.UnitTest))]
        public void APromptNameWhichIsNotUsableIsAnError()
        {
            // arrange
            string folder = CreateFolder();
            try
            {
                string withAPathInItsName = @"{ ""prompts"": [ { ""prompt-name"": ""../outside"", ""type"": ""python"", ""calls"": ""BuildPrompt.py"", ""description"": ""x"" } ] }";
                string file = CreateDefinitions(folder, withAPathInItsName);

                // act & assert
                Assert.ThrowsException<InvalidOperationException>(() => new BusinessLogicService(file, _VirtualEnvironmentsFolder));
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }

        /// <remarks>
        /// A prompt is declared like a tool, so a script which lies outside of the folder of the declaration must not be
        /// started, exactly as for a tool and a resource.
        /// </remarks>
        [TestMethod]
        [TestProperty(nameof(GRYLibrary.Core.Misc.TestKind), nameof(GRYLibrary.Core.Misc.TestKind.UnitTest))]
        public void APromptWhoseScriptLiesOutsideOfTheFolderOfTheDeclarationIsNotRun()
        {
            // arrange
            string folder = CreateFolder();
            try
            {
                string outside = @"{ ""prompts"": [ { ""prompt-name"": """ + NameOfThePrompt + @""", ""type"": ""python"", ""calls"": ""../BuildPrompt.py"", ""description"": ""x"" } ] }";
                IBusinessLogicService businessLogicService = CreateBusinessLogicService(folder, outside);

                // act
                InvalidOperationException exception = Assert.ThrowsException<InvalidOperationException>(() => businessLogicService.GetPrompt(UrlNamespace, NameOfThePrompt, new Dictionary<string, string>()));

                // assert
                StringAssert.Contains(exception.Message, "is not located inside");
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }
    }
}
