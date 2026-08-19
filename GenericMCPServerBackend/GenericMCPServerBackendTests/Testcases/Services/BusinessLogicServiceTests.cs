using GenericMCPServerBackend.Core.Model;
using GenericMCPServerBackend.Core.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;

namespace GenericMCPServerBackend.Tests.Testcases.Services
{
    /// <remarks>
    /// Every testcase brings its own script and its own declaration in a temporary folder. That is deliberate: the
    /// codeunit knows no tool, so a testcase which used one of the example-tools of the repository would test the
    /// example instead of the mechanism.
    /// </remarks>
    [TestClass]
    public class BusinessLogicServiceTests
    {
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

        /// <summary>
        /// Creates a folder which contains the script and a tool-definitions-file with the given content, and returns
        /// the path of that file.
        /// </summary>
        private static string CreateToolDefinitions(string folder, string toolDefinitions)
        {
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, "Repeat.py"), ScriptContent);
            string file = Path.Combine(folder, "Tools.json");
            File.WriteAllText(file, toolDefinitions);
            return file;
        }

        private static string CreateFolder()
        {
            return Path.Combine(Path.GetTempPath(), "GenericMCPServerBackendTests", Guid.NewGuid().ToString());
        }

        private const string OneTool = @"{
            ""tools"": [
                {
                    ""tool-name"": ""repeat"",
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
                IReadOnlyList<ToolDefinition> definitions = new BusinessLogicService(file).GetToolDefinitions();

                // assert
                Assert.AreEqual(1, definitions.Count);
                Assert.AreEqual("repeat", definitions[0].ToolName);
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
                IBusinessLogicService businessLogicService = new BusinessLogicService(CreateToolDefinitions(folder, OneTool));

                // act
                string result = businessLogicService.RunTool("repeat", new Dictionary<string, string>() { { "value", "hello" } });

                // assert
                Assert.AreEqual("hello", result);
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
                IBusinessLogicService businessLogicService = new BusinessLogicService(CreateToolDefinitions(folder, OneTool));

                // act
                InvalidOperationException exception = Assert.ThrowsException<InvalidOperationException>(() => businessLogicService.RunTool("repeat", new Dictionary<string, string>() { { "value", "fail" } }));

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
                IBusinessLogicService businessLogicService = new BusinessLogicService(CreateToolDefinitions(folder, OneTool));

                // act & assert
                Assert.ThrowsException<KeyNotFoundException>(() => businessLogicService.RunTool("something-else", new Dictionary<string, string>()));
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
            Assert.AreEqual(0, new BusinessLogicService(null).GetToolDefinitions().Count);
        }

        [TestMethod]
        [TestProperty(nameof(GRYLibrary.Core.Misc.TestKind), nameof(GRYLibrary.Core.Misc.TestKind.UnitTest))]
        public void AToolDefinitionsFileWhichWasConfiguredButDoesNotExistIsAnError()
        {
            Assert.ThrowsException<FileNotFoundException>(() => new BusinessLogicService(Path.Combine(CreateFolder(), "Tools.json")));
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
                string withoutAScript = @"{ ""tools"": [ { ""tool-name"": ""repeat"", ""description"": ""x"" } ] }";
                string file = CreateToolDefinitions(folder, withoutAScript);

                // act & assert
                Assert.ThrowsException<InvalidOperationException>(() => new BusinessLogicService(file));
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }
    }
}
