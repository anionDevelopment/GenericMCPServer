using GenericMCPServerBackend.Core.Model;
using GenericMCPServerBackend.Core.Services;
using GenericMCPServerBackend.Tests.TestUtilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Text.Json;

namespace GenericMCPServerBackend.Tests.Testcases.Services
{
    /// <summary>
    /// Covers the output-schema of a tool: what a tool declares about the shape of its answer, and what happens with
    /// an answer which fits it respectively with one which does not.
    /// </summary>
    /// <remarks>
    /// Every testcase brings its own declaration in a temporary folder, for the same reason as the testcases of the
    /// tools do: the codeunit knows no tool, so a testcase which used one of the examples of the repository would test
    /// the example instead of the mechanism.
    /// </remarks>
    [TestClass]
    public class StructuredOutputTests
    {
        /// <summary>A script which answers with a json-document. It is never run here: these testcases are about the answer and not about what produced it.</summary>
        private const string ScriptContent = @"import json
import sys

sys.stdout.write(json.dumps({'answer': 'hello', 'amount': 1}))
";

        private static readonly string _VirtualEnvironmentsFolder = CreateFolder();

        /// <summary>The schema which the tools of these testcases declare: an object with a text and a number, both required.</summary>
        private const string OutputSchema = @"{
            ""type"": ""object"",
            ""properties"": {
                ""answer"": { ""type"": ""string"" },
                ""amount"": { ""type"": ""integer"" }
            },
            ""required"": [ ""answer"", ""amount"" ]
        }";

        private const string OneToolWithAnOutputSchema = @"{
            ""tools"": [
                {
                    ""tool-name"": ""structured"",
                    ""type"": ""python"",
                    ""calls"": ""Structured.py"",
                    ""description"": ""Answers with a json-document."",
                    ""parameters"": [ ],
                    ""output-schema"": " + OutputSchema + @"
                }
            ]
        }";

        private const string OneToolWithoutAnOutputSchema = @"{
            ""tools"": [
                {
                    ""tool-name"": ""plain"",
                    ""type"": ""python"",
                    ""calls"": ""Structured.py"",
                    ""description"": ""Answers with text."",
                    ""parameters"": [ ]
                }
            ]
        }";

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

        /// <summary>
        /// Creates a folder which contains the script and a tool-definitions-file with the given content, and returns
        /// the path of that file.
        /// </summary>
        private static string CreateDefinitions(string folder, string definitions)
        {
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, "Structured.py"), ScriptContent);
            string file = Path.Combine(folder, "Tools.json");
            File.WriteAllText(file, MCPServerWrapper.WrapInAnMCPServer(definitions));
            return file;
        }

        private static IBusinessLogicService CreateBusinessLogicService(string folder, string definitions)
        {
            return new BusinessLogicService(CreateDefinitions(folder, definitions), _VirtualEnvironmentsFolder);
        }

        /// <summary>
        /// The declared tool of the given declaration, as the server offers it.
        /// </summary>
        private static ToolDefinition GetTool(string folder, string definitions)
        {
            return CreateBusinessLogicService(folder, definitions).GetMCPServers()[0].Tools[0];
        }

        /// <remarks>
        /// The declared schema is what the client is told about the answer, so it has to stay exactly what was
        /// declared, and it is what the answers are checked against.
        /// </remarks>
        [TestMethod]
        [TestProperty(nameof(GRYLibrary.Core.Misc.TestKind), nameof(GRYLibrary.Core.Misc.TestKind.UnitTest))]
        public void ADeclaredOutputSchemaBelongsToTheToolItWasDeclaredFor()
        {
            // arrange
            string folder = CreateFolder();
            try
            {
                // act
                ToolDefinition definition = GetTool(folder, OneToolWithAnOutputSchema);

                // assert
                Assert.IsTrue(definition.OutputSchema.HasValue);
                Assert.AreEqual(JsonValueKind.Object, definition.OutputSchema.Value.ValueKind);
                Assert.AreEqual("string", definition.OutputSchema.Value.GetProperty("properties").GetProperty("answer").GetProperty("type").GetString());
                Assert.IsNotNull(definition.StructuredOutput, "A tool which declared a schema checks its answers against it.");
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }

        /// <remarks>
        /// A tool which declares no schema answers with text alone, exactly as every tool did before this existed.
        /// </remarks>
        [TestMethod]
        [TestProperty(nameof(GRYLibrary.Core.Misc.TestKind), nameof(GRYLibrary.Core.Misc.TestKind.UnitTest))]
        public void AToolWithoutAnOutputSchemaHasNone()
        {
            // arrange
            string folder = CreateFolder();
            try
            {
                // act
                ToolDefinition definition = GetTool(folder, OneToolWithoutAnOutputSchema);

                // assert
                Assert.IsFalse(definition.OutputSchema.HasValue);
                Assert.IsNull(definition.StructuredOutput);
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }

        [TestMethod]
        [TestProperty(nameof(GRYLibrary.Core.Misc.TestKind), nameof(GRYLibrary.Core.Misc.TestKind.UnitTest))]
        public void AnAnswerWhichFitsTheSchemaBecomesTheStructuredContent()
        {
            // arrange
            string folder = CreateFolder();
            try
            {
                StructuredOutput structuredOutput = GetTool(folder, OneToolWithAnOutputSchema).StructuredOutput;

                // act
                JsonElement structuredContent = structuredOutput.Read(@"{ ""answer"": ""hello"", ""amount"": 42 }");

                // assert
                Assert.AreEqual("hello", structuredContent.GetProperty("answer").GetString());
                Assert.AreEqual(42, structuredContent.GetProperty("amount").GetInt32());
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }

        /// <remarks>
        /// A tool which declared a schema states that its answer is a json-document. Text which is none is therefore
        /// a failed call and not an answer which is handed over as if nothing had happened.
        /// </remarks>
        [TestMethod]
        [TestProperty(nameof(GRYLibrary.Core.Misc.TestKind), nameof(GRYLibrary.Core.Misc.TestKind.UnitTest))]
        public void AnAnswerWhichIsNoJsonDocumentIsRejected()
        {
            // arrange
            string folder = CreateFolder();
            try
            {
                StructuredOutput structuredOutput = GetTool(folder, OneToolWithAnOutputSchema).StructuredOutput;

                // act
                InvalidOperationException exception = Assert.ThrowsException<InvalidOperationException>(() => structuredOutput.Read("this is no json-document"));

                // assert
                StringAssert.Contains(exception.Message, "structured");
                StringAssert.Contains(exception.Message, "json-document", "The message has to state what was expected.");
                Assert.ThrowsException<InvalidOperationException>(() => structuredOutput.Read(string.Empty), "An answer which is empty is no json-document either.");
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }

        /// <remarks>
        /// The schema is what the client was promised, so an answer which does not keep that promise is a failed call:
        /// a client which reads the structured content as the declared shape would otherwise find something else in
        /// it.
        /// </remarks>
        [TestMethod]
        [TestProperty(nameof(GRYLibrary.Core.Misc.TestKind), nameof(GRYLibrary.Core.Misc.TestKind.UnitTest))]
        public void AnAnswerWhichDoesNotFitTheSchemaIsRejected()
        {
            // arrange
            string folder = CreateFolder();
            try
            {
                StructuredOutput structuredOutput = GetTool(folder, OneToolWithAnOutputSchema).StructuredOutput;

                // act & assert
                InvalidOperationException missingProperty = Assert.ThrowsException<InvalidOperationException>(() => structuredOutput.Read(@"{ ""answer"": ""hello"" }"));
                StringAssert.Contains(missingProperty.Message, "structured", "The message has to name the tool whose answer did not fit.");
                StringAssert.Contains(missingProperty.Message, "amount", "The message has to state what did not fit.");
                Assert.ThrowsException<InvalidOperationException>(() => structuredOutput.Read(@"{ ""answer"": ""hello"", ""amount"": ""not a number"" }"), "A value of the wrong type does not fit the schema.");
                Assert.ThrowsException<InvalidOperationException>(() => structuredOutput.Read(@"[ { ""answer"": ""hello"", ""amount"": 1 } ]"), "A list of what was declared is not what was declared.");
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }

        /// <remarks>
        /// A schema which is not a json-object is no description of an answer: a boolean-schema allows everything
        /// respectively nothing, and anything else is no schema at all. Such a declaration has to be visible when the
        /// server starts and not when a client happens to call that tool.
        /// </remarks>
        [TestMethod]
        [TestProperty(nameof(GRYLibrary.Core.Misc.TestKind), nameof(GRYLibrary.Core.Misc.TestKind.UnitTest))]
        public void AnOutputSchemaWhichIsNoJsonObjectIsAnError()
        {
            // arrange
            string folder = CreateFolder();
            try
            {
                foreach (string schema in new string[] { "true", "\"an object\"", "[ ]", "12" })
                {
                    string declaration = @"{ ""tools"": [ { ""tool-name"": ""structured"", ""type"": ""python"", ""calls"": ""Structured.py"", ""description"": ""x"", ""parameters"": [ ], ""output-schema"": " + schema + @" } ] }";
                    string file = CreateDefinitions(folder, declaration);

                    // act & assert
                    Assert.ThrowsException<InvalidOperationException>(() => new BusinessLogicService(file, _VirtualEnvironmentsFolder), $"The schema \"{schema}\" has to be rejected.");
                }
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }

        /// <remarks>
        /// A schema which can not be built is one which would never check anything, so it is rejected while the server
        /// starts instead of silently letting every answer pass.
        /// </remarks>
        [TestMethod]
        [TestProperty(nameof(GRYLibrary.Core.Misc.TestKind), nameof(GRYLibrary.Core.Misc.TestKind.UnitTest))]
        public void AnOutputSchemaWhichIsNotUsableIsAnError()
        {
            // arrange
            string folder = CreateFolder();
            try
            {
                string declaration = @"{ ""tools"": [ { ""tool-name"": ""structured"", ""type"": ""python"", ""calls"": ""Structured.py"", ""description"": ""x"", ""parameters"": [ ], ""output-schema"": { ""type"": ""object"", ""properties"": ""not a list of properties"" } } ] }";
                string file = CreateDefinitions(folder, declaration);

                // act
                InvalidOperationException exception = Assert.ThrowsException<InvalidOperationException>(() => new BusinessLogicService(file, _VirtualEnvironmentsFolder));

                // assert
                StringAssert.Contains(exception.Message, "output-schema");
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }

        /// <remarks>
        /// Only a tool answers with a result whose shape a client reads as data: a resource states the type of its
        /// content, a prompt writes the text of the prompt and a completion writes one value per line. A declaration
        /// which states a schema anyway would claim an effect which does not exist.
        /// </remarks>
        [TestMethod]
        [TestProperty(nameof(GRYLibrary.Core.Misc.TestKind), nameof(GRYLibrary.Core.Misc.TestKind.UnitTest))]
        public void AnOutputSchemaOfSomethingWhichIsNoToolIsAnError()
        {
            // arrange
            string folder = CreateFolder();
            try
            {
                string resource = @"{ ""resources"": [ { ""uri"": ""https://example.com/handbook"", ""resource-name"": ""handbook"", ""type"": ""python"", ""calls"": ""Structured.py"", ""description"": ""x"", ""mime-type"": ""text/plain"", ""output-schema"": " + OutputSchema + @" } ] }";
                string prompt = @"{ ""prompts"": [ { ""prompt-name"": ""ask"", ""type"": ""python"", ""calls"": ""Structured.py"", ""description"": ""x"", ""parameters"": [ ""topic"" ], ""output-schema"": " + OutputSchema + @" } ] }";
                string completion = @"{ ""prompts"": [ { ""prompt-name"": ""ask"", ""type"": ""python"", ""calls"": ""Structured.py"", ""description"": ""x"", ""parameters"": [ ""topic"" ], ""completions"": { ""topic"": { ""type"": ""python"", ""calls"": ""Structured.py"", ""output-schema"": " + OutputSchema + @" } } } ] }";

                // act & assert
                foreach (string declaration in new string[] { resource, prompt, completion })
                {
                    string file = CreateDefinitions(folder, declaration);
                    InvalidOperationException exception = Assert.ThrowsException<InvalidOperationException>(() => new BusinessLogicService(file, _VirtualEnvironmentsFolder), $"The declaration \"{declaration}\" has to be rejected.");
                    StringAssert.Contains(exception.Message, "output-schema");
                }
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }
    }
}
