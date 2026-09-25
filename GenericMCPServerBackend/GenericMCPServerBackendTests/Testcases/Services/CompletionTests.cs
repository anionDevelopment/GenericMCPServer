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
    /// Covers the completions: the values which an argument of a prompt can have, declared by an administrator inside
    /// the prompt that argument belongs to.
    /// </summary>
    /// <remarks>
    /// Every testcase brings its own scripts and its own declaration in a temporary folder, for the same reason as the
    /// testcases of the tools, of the resources and of the prompts do: the codeunit knows no prompt and no value of any
    /// argument, so a testcase which used one of the examples of the repository would test the example instead of the
    /// mechanism.
    /// </remarks>
    [TestClass]
    public class CompletionTests
    {
        private const string UrlNamespace = MCPServerWrapper.UrlNamespace;

        private const string NameOfThePrompt = "explain_topic";

        private const string NameOfTheArgument = "topic";

        /// <summary>A script which writes a text into which the value it got is filled.</summary>
        private const string PromptScriptContent = @"import argparse
import sys

parser = argparse.ArgumentParser()
parser.add_argument('--topic', required=True)
arguments = parser.parse_args()
sys.stdout.write('Explain ' + arguments.topic + ' to a beginner.')
";

        /// <summary>
        /// A script which writes the values which fit the value it got, one per line and each of them together with the
        /// argument it was asked about - so that a testcase sees both of the values which such a script receives.
        /// </summary>
        private const string CompletionScriptContent = @"import argparse
import sys

parser = argparse.ArgumentParser()
parser.add_argument('--argument', required=True)
parser.add_argument('--value', required=True)
arguments = parser.parse_args()
for value in ['cryptography', 'compilers', 'networking']:
    if value.startswith(arguments.value):
        sys.stdout.write(arguments.argument + ':' + value + '\n')
";

        /// <summary>A script which fails with a reason, so that a testcase can see what reaches the caller.</summary>
        private const string FailingScriptContent = @"import sys

sys.stderr.write('the values are not available')
sys.exit(1)
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

        private const string OnePromptWithACompletion = @"{
            ""prompts"": [
                {
                    ""prompt-name"": """ + NameOfThePrompt + @""",
                    ""type"": ""python"",
                    ""calls"": ""BuildPrompt.py"",
                    ""description"": ""Asks for an explanation of a topic."",
                    ""parameters"": [ """ + NameOfTheArgument + @""" ],
                    ""completions"": {
                        """ + NameOfTheArgument + @""": {
                            ""type"": ""python"",
                            ""calls"": ""CompleteTopic.py""
                        }
                    }
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
            File.WriteAllText(Path.Combine(folder, "BuildPrompt.py"), PromptScriptContent);
            File.WriteAllText(Path.Combine(folder, "CompleteTopic.py"), CompletionScriptContent);
            File.WriteAllText(Path.Combine(folder, "FailingCompletion.py"), FailingScriptContent);
            string file = Path.Combine(folder, "Tools.json");
            File.WriteAllText(file, MCPServerWrapper.WrapInAnMCPServer(definitions));
            return file;
        }

        private static IBusinessLogicService CreateBusinessLogicService(string folder, string definitions)
        {
            return new BusinessLogicService(CreateDefinitions(folder, definitions), _VirtualEnvironmentsFolder);
        }

        /// <summary>
        /// A declaration which states a completion for an argument which the prompt does not take describes a script
        /// nobody would ever call, so it has to be visible when the server starts.
        /// </summary>
        private static string DeclareCompletion(string argumentOfThePrompt, string completedArgument, string completion)
        {
            return @"{
                ""prompts"": [
                    {
                        ""prompt-name"": """ + NameOfThePrompt + @""",
                        ""type"": ""python"",
                        ""calls"": ""BuildPrompt.py"",
                        ""description"": ""x"",
                        ""parameters"": [ """ + argumentOfThePrompt + @""" ],
                        ""completions"": { """ + completedArgument + @""": " + completion + @" }
                    }
                ]
            }";
        }

        [TestMethod]
        [TestProperty(nameof(GRYLibrary.Core.Misc.TestKind), nameof(GRYLibrary.Core.Misc.TestKind.UnitTest))]
        public void ADeclaredCompletionBelongsToTheArgumentItWasDeclaredFor()
        {
            // arrange
            string folder = CreateFolder();
            try
            {
                // act
                PromptDefinition prompt = CreateBusinessLogicService(folder, OnePromptWithACompletion).GetMCPServers()[0].Prompts[0];

                // assert
                Assert.AreEqual(1, prompt.Completions.Count);
                CompletionDefinition completion = prompt.Completions[NameOfTheArgument];
                Assert.AreEqual(NameOfThePrompt, completion.PromptName);
                Assert.AreEqual(NameOfTheArgument, completion.ArgumentName);
                Assert.AreEqual(ToolTypes.Python, completion.Type);
                // The script is told which argument it is asked about and which value was entered so far; both belong
                // to the mechanism and are therefore not declared.
                CollectionAssert.AreEqual(new string[] { CompletionDefinition.ArgumentParameterName, CompletionDefinition.ValueParameterName }, new List<string>(completion.Parameters));
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }

        /// <remarks>
        /// The script receives the argument it is asked about and the value which was entered so far, and every
        /// non-empty line it writes is one value - which is what lets it decide which values it offers.
        /// </remarks>
        [TestMethod]
        [TestProperty(nameof(GRYLibrary.Core.Misc.TestKind), nameof(GRYLibrary.Core.Misc.TestKind.UnitTest))]
        public void AskingForTheValuesOfAnArgumentAnswersWithTheLinesItsScriptWrote()
        {
            // arrange
            string folder = CreateFolder();
            try
            {
                IBusinessLogicService businessLogicService = CreateBusinessLogicService(folder, OnePromptWithACompletion);

                // act
                IReadOnlyList<string> everything = businessLogicService.GetCompletions(UrlNamespace, NameOfThePrompt, NameOfTheArgument, string.Empty);
                IReadOnlyList<string> beginningWithC = businessLogicService.GetCompletions(UrlNamespace, NameOfThePrompt, NameOfTheArgument, "c");

                // assert
                CollectionAssert.AreEqual(new string[] { "topic:cryptography", "topic:compilers", "topic:networking" }, new List<string>(everything), "Every non-empty line is one value, and the script is told which argument it is asked about.");
                CollectionAssert.AreEqual(new string[] { "topic:cryptography", "topic:compilers" }, new List<string>(beginningWithC), "The value which was entered so far reaches the script, which is what it decides its answer with.");
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }

        /// <remarks>
        /// "Which values are possible" is a question a client may ask about every argument, and "none are known here"
        /// is an answer to it and not a failure.
        /// </remarks>
        [TestMethod]
        [TestProperty(nameof(GRYLibrary.Core.Misc.TestKind), nameof(GRYLibrary.Core.Misc.TestKind.UnitTest))]
        public void AnArgumentWithoutACompletionAndAPromptWhichIsNotDeclaredAnswerWithNoValue()
        {
            // arrange
            string folder = CreateFolder();
            try
            {
                IBusinessLogicService businessLogicService = CreateBusinessLogicService(folder, OnePromptWithACompletion);

                // act
                IReadOnlyList<string> ofAnotherArgument = businessLogicService.GetCompletions(UrlNamespace, NameOfThePrompt, "another_argument", string.Empty);
                IReadOnlyList<string> ofAnotherPrompt = businessLogicService.GetCompletions(UrlNamespace, "another_prompt", NameOfTheArgument, string.Empty);

                // assert
                Assert.AreEqual(0, ofAnotherArgument.Count);
                Assert.AreEqual(0, ofAnotherPrompt.Count);
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }

        /// <remarks>
        /// What a script reported is what a caller needs to learn why it did not receive the values it asked for.
        /// </remarks>
        [TestMethod]
        [TestProperty(nameof(GRYLibrary.Core.Misc.TestKind), nameof(GRYLibrary.Core.Misc.TestKind.UnitTest))]
        public void ACompletionWhoseScriptFailsReportsTheReasonOfTheScript()
        {
            // arrange
            string folder = CreateFolder();
            try
            {
                IBusinessLogicService businessLogicService = CreateBusinessLogicService(folder, DeclareCompletion(NameOfTheArgument, NameOfTheArgument, @"{ ""type"": ""python"", ""calls"": ""FailingCompletion.py"" }"));

                // act
                InvalidOperationException exception = Assert.ThrowsException<InvalidOperationException>(() => businessLogicService.GetCompletions(UrlNamespace, NameOfThePrompt, NameOfTheArgument, string.Empty));

                // assert
                StringAssert.Contains(exception.Message, "the values are not available");
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }

        /// <remarks>
        /// A completion is declared like a prompt, so a script which lies outside of the folder of the declaration must
        /// not be started, exactly as for a tool, a resource and a prompt.
        /// </remarks>
        [TestMethod]
        [TestProperty(nameof(GRYLibrary.Core.Misc.TestKind), nameof(GRYLibrary.Core.Misc.TestKind.UnitTest))]
        public void ACompletionWhoseScriptLiesOutsideOfTheFolderOfTheDeclarationIsNotRun()
        {
            // arrange
            string folder = CreateFolder();
            try
            {
                IBusinessLogicService businessLogicService = CreateBusinessLogicService(folder, DeclareCompletion(NameOfTheArgument, NameOfTheArgument, @"{ ""type"": ""python"", ""calls"": ""../CompleteTopic.py"" }"));

                // act
                InvalidOperationException exception = Assert.ThrowsException<InvalidOperationException>(() => businessLogicService.GetCompletions(UrlNamespace, NameOfThePrompt, NameOfTheArgument, string.Empty));

                // assert
                StringAssert.Contains(exception.Message, "is not located inside");
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }

        /// <remarks>
        /// A completion belongs to one argument of one prompt. A declaration which names an argument that prompt does
        /// not take describes a script which nobody would ever call, so it is a mistake which has to be visible when
        /// the server starts.
        /// </remarks>
        [TestMethod]
        [TestProperty(nameof(GRYLibrary.Core.Misc.TestKind), nameof(GRYLibrary.Core.Misc.TestKind.UnitTest))]
        public void ACompletionForAnArgumentWhichThePromptDoesNotDeclareIsAnError()
        {
            // arrange
            string folder = CreateFolder();
            try
            {
                string file = CreateDefinitions(folder, DeclareCompletion(NameOfTheArgument, "another_argument", @"{ ""type"": ""python"", ""calls"": ""CompleteTopic.py"" }"));

                // act
                InvalidOperationException exception = Assert.ThrowsException<InvalidOperationException>(() => new BusinessLogicService(file, _VirtualEnvironmentsFolder));

                // assert
                StringAssert.Contains(exception.Message, "another_argument");
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }

        /// <remarks>
        /// Such a declaration takes the address it requests either from the client (a tool) or from its own uri (a
        /// resource), and a completion has neither: it is asked which values an argument of a prompt can have.
        /// </remarks>
        [TestMethod]
        [TestProperty(nameof(GRYLibrary.Core.Misc.TestKind), nameof(GRYLibrary.Core.Misc.TestKind.UnitTest))]
        public void AWebRequestCompletionIsAnError()
        {
            // arrange
            string folder = CreateFolder();
            try
            {
                string file = CreateDefinitions(folder, DeclareCompletion(NameOfTheArgument, NameOfTheArgument, @"{ ""type"": ""web-request"", ""allowed-urls"": [ ""example\\.com/.*"" ] }"));

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
        /// A declaration which is incomplete or which states something that has no meaning for a completion has to be
        /// visible when the server starts and not when a client happens to ask for the values of the affected argument.
        /// </remarks>
        [TestMethod]
        [TestProperty(nameof(GRYLibrary.Core.Misc.TestKind), nameof(GRYLibrary.Core.Misc.TestKind.UnitTest))]
        public void AnIncompleteOrContradictoryDeclarationOfACompletionIsAnError()
        {
            // arrange
            string folder = CreateFolder();
            try
            {
                string withoutAType = @"{ ""calls"": ""CompleteTopic.py"" }";
                string withoutAScript = @"{ ""type"": ""python"" }";
                // A client asks for the possible values of an argument it already chose and never chooses a completion
                // itself, so a description would be a text which nobody reads.
                string withADescription = @"{ ""type"": ""python"", ""calls"": ""CompleteTopic.py"", ""description"": ""The topics."" }";
                // Which values such a script receives is decided by the mechanism and not by a declaration.
                string withParameters = @"{ ""type"": ""python"", ""calls"": ""CompleteTopic.py"", ""parameters"": [ ""something"" ] }";
                // Only a declaration which requests an address sends headers.
                string withHeaders = @"{ ""type"": ""python"", ""calls"": ""CompleteTopic.py"", ""headers"": { ""Accept"": ""text/plain"" } }";

                // act & assert
                foreach (string completion in new string[] { withoutAType, withoutAScript, withADescription, withParameters, withHeaders })
                {
                    string file = CreateDefinitions(folder, DeclareCompletion(NameOfTheArgument, NameOfTheArgument, completion));
                    Assert.ThrowsException<InvalidOperationException>(() => new BusinessLogicService(file, _VirtualEnvironmentsFolder), $"The completion \"{completion}\" has to be rejected.");
                }
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }

        /// <remarks>
        /// The name of the argument is a part of the name of the folder of the virtual environment of a
        /// python-completion, so a name which contains a path-separator is rejected instead of turning into a folder
        /// somewhere else.
        /// </remarks>
        [TestMethod]
        [TestProperty(nameof(GRYLibrary.Core.Misc.TestKind), nameof(GRYLibrary.Core.Misc.TestKind.UnitTest))]
        public void ACompletionForAnArgumentWhoseNameIsNotUsableIsAnError()
        {
            // arrange
            string folder = CreateFolder();
            try
            {
                string file = CreateDefinitions(folder, DeclareCompletion("../outside", "../outside", @"{ ""type"": ""python"", ""calls"": ""CompleteTopic.py"" }"));

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
