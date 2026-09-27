using GenericMCPServerBackend.Tests.TestUtilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;

namespace GenericMCPServerBackend.Tests.Testcases.Integration
{
    /// <summary>
    /// Exercises the MCP-endpoint over real http, the way a MCP-client does it.
    ///
    /// The testcases of the business-logic call the c#-objects directly and therefore say nothing about whether the
    /// protocol-layer around them works: whether the endpoint is mapped at all, whether the declared tools reach the
    /// wire and whether the answer of a script arrives at the caller. That is what these testcases cover.
    /// </summary>
    /// <remarks>
    /// Every testcase of this class starts its own in-process server, which binds a fixed port. Two of them must
    /// therefore never run at the same time, which the lock below guarantees independently of how the test-runner is
    /// configured to parallelize.
    /// </remarks>
    [TestClass]
    public class MCPEndpointTests
    {
        [TestInitialize]
        public void TestInitialize()
        {
            IntegrationTestServerLock.Enter();
        }

        [TestCleanup]
        public void TestCleanup()
        {
            IntegrationTestServerLock.Exit();
        }

        /// <remarks>
        /// A tool which is not declared as anonymous belongs to the clients which authenticated themselves. A token
        /// which nobody created is worth as much as no token at all.
        /// </remarks>
        [TestMethod]
        [TestProperty(nameof(GRYLibrary.Core.Misc.TestKind), nameof(GRYLibrary.Core.Misc.TestKind.IntegrationTest))]
        public void WithoutAValidAccessTokenAToolIsNotReachable()
        {
            // arrange
            using IntegrationTestFramework framework = new IntegrationTestFramework();
            string message = @"{""jsonrpc"":""2.0"",""id"":1,""method"":""tools/call"",""params"":{""name"":""" + ExampleDataCreator.ExampleToolName + @""",""arguments"":{""" + ExampleDataCreator.ExampleToolParameterName + @""":""hello-world""}}}";

            // act
            (HttpStatusCode withoutAToken, string _) = framework.SendMCPMessageAndGetStatusCode(message, ExampleDataCreator.UrlNamespace, null);
            (HttpStatusCode withAnUnknownToken, string _) = framework.SendMCPMessageAndGetStatusCode(message, ExampleDataCreator.UrlNamespace, "gmcp_0000000000000000000000000000000000000000000000000000000000000000");

            // assert
            Assert.AreEqual(HttpStatusCode.Unauthorized, withoutAToken);
            Assert.AreEqual(HttpStatusCode.Unauthorized, withAnUnknownToken);
        }

        /// <remarks>
        /// An mcp-server which is declared as anonymous is usable by everybody, and it is separated from the other one:
        /// a caller without a token reaches it and sees exactly its tools, while the mcp-server which needs a token
        /// stays closed for the same caller.
        /// </remarks>
        [TestMethod]
        [TestProperty(nameof(GRYLibrary.Core.Misc.TestKind), nameof(GRYLibrary.Core.Misc.TestKind.IntegrationTest))]
        public void AnMCPServerWhichIsDeclaredAsAnonymousIsUsableWithoutAnAccessToken()
        {
            // arrange
            using IntegrationTestFramework framework = new IntegrationTestFramework();

            // act
            (HttpStatusCode statusCodeOfTheList, string list) = framework.SendMCPMessageAndGetStatusCode(@"{""jsonrpc"":""2.0"",""id"":1,""method"":""tools/list""}", ExampleDataCreator.AnonymousUrlNamespace, null);
            (HttpStatusCode statusCodeOfTheCall, string answer) = framework.SendMCPMessageAndGetStatusCode(@"{""jsonrpc"":""2.0"",""id"":2,""method"":""tools/call"",""params"":{""name"":""" + ExampleDataCreator.AnonymousExampleToolName + @""",""arguments"":{""" + ExampleDataCreator.ExampleToolParameterName + @""":""hello-world""}}}", ExampleDataCreator.AnonymousUrlNamespace, null);
            (HttpStatusCode statusCodeOfTheForbiddenCall, string _) = framework.SendMCPMessageAndGetStatusCode(@"{""jsonrpc"":""2.0"",""id"":3,""method"":""tools/list""}", ExampleDataCreator.UrlNamespace, null);

            // assert
            Assert.AreEqual(HttpStatusCode.OK, statusCodeOfTheList);
            JsonElement tools = JsonDocument.Parse(IntegrationTestFramework.GetPayloadOfAnswer(list)).RootElement.GetProperty("result").GetProperty("tools");
            Assert.AreEqual(1, tools.GetArrayLength(), "An mcp-server offers exactly its own tools.");
            Assert.AreEqual(ExampleDataCreator.AnonymousExampleToolName, tools[0].GetProperty("name").GetString());
            Assert.AreEqual(HttpStatusCode.OK, statusCodeOfTheCall);
            Assert.AreEqual("the answer is hello-world", JsonDocument.Parse(IntegrationTestFramework.GetPayloadOfAnswer(answer)).RootElement.GetProperty("result").GetProperty("content")[0].GetProperty("text").GetString());
            Assert.AreEqual(HttpStatusCode.Unauthorized, statusCodeOfTheForbiddenCall, "The mcp-server which is not anonymous stays behind the token.");
        }

        [TestMethod]
        [TestProperty(nameof(GRYLibrary.Core.Misc.TestKind), nameof(GRYLibrary.Core.Misc.TestKind.IntegrationTest))]
        public void TheDeclaredToolsAreOfferedOverHTTP()
        {
            // arrange
            using IntegrationTestFramework framework = new IntegrationTestFramework();

            // act
            string answer = framework.SendMCPMessage(@"{""jsonrpc"":""2.0"",""id"":1,""method"":""tools/list""}");

            // assert
            JsonElement tools = JsonDocument.Parse(IntegrationTestFramework.GetPayloadOfAnswer(answer)).RootElement.GetProperty("result").GetProperty("tools");
            Assert.AreEqual(3, tools.GetArrayLength(), "The mcp-server has to offer exactly the tools which the example-data declares for it.");
            JsonElement tool = tools.EnumerateArray().Single(entry => entry.GetProperty("name").GetString() == ExampleDataCreator.ExampleToolName);
            Assert.AreEqual("Answers with the value it got.", tool.GetProperty("description").GetString());
            Assert.IsTrue(tool.GetProperty("inputSchema").GetProperty("properties").TryGetProperty(ExampleDataCreator.ExampleToolParameterName, out JsonElement _), "The declared parameter has to be part of the schema which the client gets.");
        }

        /// <remarks>
        /// The declared output-schema is what tells a client that the answer of that tool is data of a known shape,
        /// so it has to reach the client with the tool. A tool which declares none offers none, which is what keeps a
        /// client from expecting data where there is text.
        /// </remarks>
        [TestMethod]
        [TestProperty(nameof(GRYLibrary.Core.Misc.TestKind), nameof(GRYLibrary.Core.Misc.TestKind.IntegrationTest))]
        public void TheDeclaredOutputSchemaIsOfferedWithTheToolOverHTTP()
        {
            // arrange
            using IntegrationTestFramework framework = new IntegrationTestFramework();

            // act
            string answer = framework.SendMCPMessage(@"{""jsonrpc"":""2.0"",""id"":1,""method"":""tools/list""}");

            // assert
            JsonElement tools = JsonDocument.Parse(IntegrationTestFramework.GetPayloadOfAnswer(answer)).RootElement.GetProperty("result").GetProperty("tools");
            JsonElement toolWithASchema = tools.EnumerateArray().Single(entry => entry.GetProperty("name").GetString() == ExampleDataCreator.StructuredExampleToolName);
            JsonElement outputSchema = toolWithASchema.GetProperty("outputSchema");
            Assert.AreEqual("object", outputSchema.GetProperty("type").GetString());
            Assert.AreEqual("string", outputSchema.GetProperty("properties").GetProperty("answer").GetProperty("type").GetString());
            Assert.AreEqual("integer", outputSchema.GetProperty("properties").GetProperty("length").GetProperty("type").GetString());
            JsonElement toolWithoutASchema = tools.EnumerateArray().Single(entry => entry.GetProperty("name").GetString() == ExampleDataCreator.ExampleToolName);
            Assert.IsFalse(toolWithoutASchema.TryGetProperty("outputSchema", out JsonElement _), "A tool which declares no output-schema must not offer one.");
        }

        /// <remarks>
        /// A tool which declared the shape of its answer hands that answer over as data as well, so that a client
        /// does not have to parse the text to get it. The text stays part of the answer, because a client which reads
        /// no structured content receives what it received before.
        /// </remarks>
        [TestMethod]
        [TestProperty(nameof(GRYLibrary.Core.Misc.TestKind), nameof(GRYLibrary.Core.Misc.TestKind.IntegrationTest))]
        public void CallingAToolWithAnOutputSchemaAnswersWithStructuredContentOverHTTP()
        {
            // arrange
            using IntegrationTestFramework framework = new IntegrationTestFramework();

            // act
            (HttpStatusCode statusCode, string answer) = framework.SendMCPMessageAndGetStatusCode(@"{""jsonrpc"":""2.0"",""id"":2,""method"":""tools/call"",""params"":{""name"":""" + ExampleDataCreator.StructuredExampleToolName + @""",""arguments"":{""" + ExampleDataCreator.ExampleToolParameterName + @""":""hello-world""}}}");

            // assert
            Assert.AreEqual(HttpStatusCode.OK, statusCode, "A call which worked is a normal answer of the server.");
            JsonElement result = JsonDocument.Parse(IntegrationTestFramework.GetPayloadOfAnswer(answer)).RootElement.GetProperty("result");
            Assert.IsFalse(result.GetProperty("isError").GetBoolean());
            JsonElement structuredContent = result.GetProperty("structuredContent");
            Assert.AreEqual("the answer is hello-world", structuredContent.GetProperty("answer").GetString());
            Assert.AreEqual("hello-world".Length, structuredContent.GetProperty("length").GetInt32());
            string? whatTheScriptWrote = result.GetProperty("content")[0].GetProperty("text").GetString();
            Assert.IsNotNull(whatTheScriptWrote, "What the script wrote stays part of the answer as text.");
            Assert.AreEqual("the answer is hello-world", JsonDocument.Parse(whatTheScriptWrote).RootElement.GetProperty("answer").GetString());
        }

        /// <remarks>
        /// A tool which declared a schema promised a json-document of that shape. An answer which is none is a failed
        /// call and not something which is handed over as if it were the promised data - a client which reads the
        /// structured content would otherwise find nothing where it expected the answer.
        /// </remarks>
        [TestMethod]
        [TestProperty(nameof(GRYLibrary.Core.Misc.TestKind), nameof(GRYLibrary.Core.Misc.TestKind.IntegrationTest))]
        public void AnAnswerWhichDoesNotFitTheOutputSchemaFailsTheCallOverHTTP()
        {
            // arrange
            using IntegrationTestFramework framework = new IntegrationTestFramework();

            // act: for this value the script answers with something which is no json-document
            (HttpStatusCode statusCode, string answer) = framework.SendMCPMessageAndGetStatusCode(@"{""jsonrpc"":""2.0"",""id"":3,""method"":""tools/call"",""params"":{""name"":""" + ExampleDataCreator.StructuredExampleToolName + @""",""arguments"":{""" + ExampleDataCreator.ExampleToolParameterName + @""":""" + ExampleDataCreator.ValueWhichBreaksTheStructuredAnswer + @"""}}}");

            // assert
            Assert.AreEqual(HttpStatusCode.BadGateway, statusCode, "The failure comes from the program behind this server, exactly like a script which ended with an exit-code which is not 0.");
            JsonElement result = JsonDocument.Parse(IntegrationTestFramework.GetPayloadOfAnswer(answer)).RootElement.GetProperty("result");
            Assert.IsTrue(result.GetProperty("isError").GetBoolean());
            Assert.IsFalse(result.TryGetProperty("structuredContent", out JsonElement _), "A failed call carries no structured content.");
            StringAssert.Contains(result.GetProperty("content")[0].GetProperty("text").GetString(), "json-document", "The message has to state what was wrong with the answer.");
        }

        [TestMethod]
        [TestProperty(nameof(GRYLibrary.Core.Misc.TestKind), nameof(GRYLibrary.Core.Misc.TestKind.IntegrationTest))]
        public void CallingAToolOverHTTPAnswersWithWhatItsScriptWrote()
        {
            // arrange
            using IntegrationTestFramework framework = new IntegrationTestFramework();

            // act
            (HttpStatusCode statusCode, string answer) = framework.SendMCPMessageAndGetStatusCode(@"{""jsonrpc"":""2.0"",""id"":2,""method"":""tools/call"",""params"":{""name"":""" + ExampleDataCreator.ExampleToolName + @""",""arguments"":{""" + ExampleDataCreator.ExampleToolParameterName + @""":""hello-world""}}}");

            // assert
            Assert.AreEqual(HttpStatusCode.OK, statusCode, "A call which worked is a normal answer of the server.");
            JsonElement result = JsonDocument.Parse(IntegrationTestFramework.GetPayloadOfAnswer(answer)).RootElement.GetProperty("result");
            Assert.IsFalse(result.GetProperty("isError").GetBoolean());
            Assert.AreEqual("the answer is hello-world", result.GetProperty("content")[0].GetProperty("text").GetString());
        }

        /// <remarks>
        /// A value which the script rejects has to arrive at the caller as a failed call whose message states the
        /// reason: a model which called the tool can only correct itself if it learns what was wrong. The whole answer
        /// additionally carries the status-code 502, because the failure comes from the program behind the server and
        /// this server is the gateway to it.
        /// </remarks>
        [TestMethod]
        [TestProperty(nameof(GRYLibrary.Core.Misc.TestKind), nameof(GRYLibrary.Core.Misc.TestKind.IntegrationTest))]
        public void AValueWhichTheScriptRejectsAnswersWithTheReasonAnd502OverHTTP()
        {
            // arrange
            using IntegrationTestFramework framework = new IntegrationTestFramework();

            // act: the script only accepts lowercase letters, digits and hyphens
            (HttpStatusCode statusCode, string answer) = framework.SendMCPMessageAndGetStatusCode(@"{""jsonrpc"":""2.0"",""id"":3,""method"":""tools/call"",""params"":{""name"":""" + ExampleDataCreator.ExampleToolName + @""",""arguments"":{""" + ExampleDataCreator.ExampleToolParameterName + @""":""NOT ALLOWED""}}}");

            // assert
            Assert.AreEqual(HttpStatusCode.BadGateway, statusCode);
            JsonElement result = JsonDocument.Parse(IntegrationTestFramework.GetPayloadOfAnswer(answer)).RootElement.GetProperty("result");
            Assert.IsTrue(result.GetProperty("isError").GetBoolean(), "The message of the failure has to stay part of the answer, so that the caller learns what was wrong.");
            StringAssert.Contains(result.GetProperty("content")[0].GetProperty("text").GetString(), "lowercase letters");
        }

        /// <remarks>
        /// A resource is offered with exactly what its declaration states, so that a client can decide by the list
        /// alone whether it wants to read it.
        /// </remarks>
        [TestMethod]
        [TestProperty(nameof(GRYLibrary.Core.Misc.TestKind), nameof(GRYLibrary.Core.Misc.TestKind.IntegrationTest))]
        public void TheDeclaredResourcesAreOfferedOverHTTP()
        {
            // arrange
            using IntegrationTestFramework framework = new IntegrationTestFramework();

            // act
            string answer = framework.SendMCPMessage(@"{""jsonrpc"":""2.0"",""id"":1,""method"":""resources/list""}");

            // assert
            JsonElement resources = JsonDocument.Parse(IntegrationTestFramework.GetPayloadOfAnswer(answer)).RootElement.GetProperty("result").GetProperty("resources");
            Assert.AreEqual(1, resources.GetArrayLength(), "The mcp-server has to offer exactly the resources which the example-data declares for it.");
            Assert.AreEqual(ExampleDataCreator.ExampleResourceUri, resources[0].GetProperty("uri").GetString());
            Assert.AreEqual(ExampleDataCreator.ExampleResourceName, resources[0].GetProperty("name").GetString());
            Assert.AreEqual(ExampleDataCreator.ExampleResourceMimeType, resources[0].GetProperty("mimeType").GetString());
            Assert.AreEqual("The handbook of the team.", resources[0].GetProperty("description").GetString());
        }

        /// <remarks>
        /// Reading a resource hands over what the script wrote, together with the uri it was read by and the declared
        /// type of the content.
        /// </remarks>
        [TestMethod]
        [TestProperty(nameof(GRYLibrary.Core.Misc.TestKind), nameof(GRYLibrary.Core.Misc.TestKind.IntegrationTest))]
        public void ReadingAResourceOverHTTPAnswersWithWhatItsScriptWrote()
        {
            // arrange
            using IntegrationTestFramework framework = new IntegrationTestFramework();

            // act
            (HttpStatusCode statusCode, string answer) = framework.SendMCPMessageAndGetStatusCode(@"{""jsonrpc"":""2.0"",""id"":2,""method"":""resources/read"",""params"":{""uri"":""" + ExampleDataCreator.ExampleResourceUri + @"""}}");

            // assert
            Assert.AreEqual(HttpStatusCode.OK, statusCode);
            JsonElement contents = JsonDocument.Parse(IntegrationTestFramework.GetPayloadOfAnswer(answer)).RootElement.GetProperty("result").GetProperty("contents");
            Assert.AreEqual(1, contents.GetArrayLength());
            Assert.AreEqual(ExampleDataCreator.ExampleResourceUri, contents[0].GetProperty("uri").GetString());
            Assert.AreEqual(ExampleDataCreator.ExampleResourceMimeType, contents[0].GetProperty("mimeType").GetString());
            Assert.AreEqual($"the content of {ExampleDataCreator.ExampleResourceUri}", contents[0].GetProperty("text").GetString());
        }

        /// <remarks>
        /// A uri which no declaration states is not a resource of this server. The answer is an error of the protocol
        /// and not a content, because a client which asked for a document must not receive the reason of a failure as
        /// if it were that document.
        /// </remarks>
        [TestMethod]
        [TestProperty(nameof(GRYLibrary.Core.Misc.TestKind), nameof(GRYLibrary.Core.Misc.TestKind.IntegrationTest))]
        public void ReadingAResourceWhichIsNotDeclaredAnswersWithAnError()
        {
            // arrange
            using IntegrationTestFramework framework = new IntegrationTestFramework();

            // act
            (HttpStatusCode _, string answer) = framework.SendMCPMessageAndGetStatusCode(@"{""jsonrpc"":""2.0"",""id"":3,""method"":""resources/read"",""params"":{""uri"":""example://documents/something-else""}}");

            // assert
            JsonElement root = JsonDocument.Parse(IntegrationTestFramework.GetPayloadOfAnswer(answer)).RootElement;
            Assert.IsFalse(root.TryGetProperty("result", out JsonElement _), "A uri which is not declared must not be answered with a content.");
            Assert.IsTrue(root.TryGetProperty("error", out JsonElement _));
        }

        /// <remarks>
        /// An mcp-server which declares no resource does not claim the resources-capability of the protocol at all, so
        /// a client of it never asks for a list which is empty by definition.
        /// </remarks>
        [TestMethod]
        [TestProperty(nameof(GRYLibrary.Core.Misc.TestKind), nameof(GRYLibrary.Core.Misc.TestKind.IntegrationTest))]
        public void AnMCPServerWhichDeclaresNoResourceOffersNoResourcesAtAll()
        {
            // arrange
            using IntegrationTestFramework framework = new IntegrationTestFramework();

            // act: the anonymous mcp-server of the example-data declares a tool and no resource
            (HttpStatusCode _, string answer) = framework.SendMCPMessageAndGetStatusCode(@"{""jsonrpc"":""2.0"",""id"":1,""method"":""resources/list""}", ExampleDataCreator.AnonymousUrlNamespace, null);

            // assert
            JsonElement root = JsonDocument.Parse(IntegrationTestFramework.GetPayloadOfAnswer(answer)).RootElement;
            Assert.IsFalse(root.TryGetProperty("result", out JsonElement _), "An mcp-server without resources must not answer a list of them.");
            Assert.IsTrue(root.TryGetProperty("error", out JsonElement _));
        }

        /// <remarks>
        /// A prompt is offered with exactly what its declaration states, so that a client can decide by the list alone
        /// whether it wants to ask for it and which arguments it has to state.
        /// </remarks>
        [TestMethod]
        [TestProperty(nameof(GRYLibrary.Core.Misc.TestKind), nameof(GRYLibrary.Core.Misc.TestKind.IntegrationTest))]
        public void TheDeclaredPromptsAreOfferedOverHTTP()
        {
            // arrange
            using IntegrationTestFramework framework = new IntegrationTestFramework();

            // act
            string answer = framework.SendMCPMessage(@"{""jsonrpc"":""2.0"",""id"":1,""method"":""prompts/list""}");

            // assert
            JsonElement prompts = JsonDocument.Parse(IntegrationTestFramework.GetPayloadOfAnswer(answer)).RootElement.GetProperty("result").GetProperty("prompts");
            Assert.AreEqual(1, prompts.GetArrayLength(), "The mcp-server has to offer exactly the prompts which the example-data declares for it.");
            Assert.AreEqual(ExampleDataCreator.ExamplePromptName, prompts[0].GetProperty("name").GetString());
            Assert.AreEqual("Asks for an explanation of a topic.", prompts[0].GetProperty("description").GetString());
            JsonElement arguments = prompts[0].GetProperty("arguments");
            Assert.AreEqual(1, arguments.GetArrayLength(), "The declared arguments have to be part of what the client gets.");
            Assert.AreEqual(ExampleDataCreator.ExamplePromptArgumentName, arguments[0].GetProperty("name").GetString());
            Assert.IsTrue(arguments[0].GetProperty("required").GetBoolean(), "Every declared argument of a prompt is a required one.");
        }

        /// <remarks>
        /// Asking for a prompt hands over the text which its script wrote for the arguments the client stated, as one
        /// message of the role "user": a prompt of this server is a text which a client hands to its model.
        /// </remarks>
        [TestMethod]
        [TestProperty(nameof(GRYLibrary.Core.Misc.TestKind), nameof(GRYLibrary.Core.Misc.TestKind.IntegrationTest))]
        public void AskingForAPromptOverHTTPAnswersWithWhatItsScriptWrote()
        {
            // arrange
            using IntegrationTestFramework framework = new IntegrationTestFramework();

            // act
            (HttpStatusCode statusCode, string answer) = framework.SendMCPMessageAndGetStatusCode(@"{""jsonrpc"":""2.0"",""id"":2,""method"":""prompts/get"",""params"":{""name"":""" + ExampleDataCreator.ExamplePromptName + @""",""arguments"":{""" + ExampleDataCreator.ExamplePromptArgumentName + @""":""cryptography""}}}");

            // assert
            Assert.AreEqual(HttpStatusCode.OK, statusCode);
            JsonElement messages = JsonDocument.Parse(IntegrationTestFramework.GetPayloadOfAnswer(answer)).RootElement.GetProperty("result").GetProperty("messages");
            Assert.AreEqual(1, messages.GetArrayLength());
            Assert.AreEqual("user", messages[0].GetProperty("role").GetString());
            Assert.AreEqual("Explain cryptography to a beginner.", messages[0].GetProperty("content").GetProperty("text").GetString());
        }

        /// <remarks>
        /// A name which no declaration states is not a prompt of this server. The answer is an error of the protocol
        /// and not a message, because a client which asked for a prompt must not hand the reason of a failure to its
        /// model as if it were that prompt.
        /// </remarks>
        [TestMethod]
        [TestProperty(nameof(GRYLibrary.Core.Misc.TestKind), nameof(GRYLibrary.Core.Misc.TestKind.IntegrationTest))]
        public void AskingForAPromptWhichIsNotDeclaredAnswersWithAnError()
        {
            // arrange
            using IntegrationTestFramework framework = new IntegrationTestFramework();

            // act
            (HttpStatusCode _, string answer) = framework.SendMCPMessageAndGetStatusCode(@"{""jsonrpc"":""2.0"",""id"":3,""method"":""prompts/get"",""params"":{""name"":""another_prompt""}}");

            // assert
            JsonElement root = JsonDocument.Parse(IntegrationTestFramework.GetPayloadOfAnswer(answer)).RootElement;
            Assert.IsFalse(root.TryGetProperty("result", out JsonElement _), "A name which is not declared must not be answered with a prompt.");
            Assert.IsTrue(root.TryGetProperty("error", out JsonElement _));
        }

        /// <remarks>
        /// An mcp-server which declares no prompt does not claim the prompts-capability of the protocol at all, so a
        /// client of it never asks for a list which is empty by definition.
        /// </remarks>
        [TestMethod]
        [TestProperty(nameof(GRYLibrary.Core.Misc.TestKind), nameof(GRYLibrary.Core.Misc.TestKind.IntegrationTest))]
        public void AnMCPServerWhichDeclaresNoPromptOffersNoPromptsAtAll()
        {
            // arrange
            using IntegrationTestFramework framework = new IntegrationTestFramework();

            // act: the anonymous mcp-server of the example-data declares a tool and no prompt
            (HttpStatusCode _, string answer) = framework.SendMCPMessageAndGetStatusCode(@"{""jsonrpc"":""2.0"",""id"":1,""method"":""prompts/list""}", ExampleDataCreator.AnonymousUrlNamespace, null);

            // assert
            JsonElement root = JsonDocument.Parse(IntegrationTestFramework.GetPayloadOfAnswer(answer)).RootElement;
            Assert.IsFalse(root.TryGetProperty("result", out JsonElement _), "An mcp-server without prompts must not answer a list of them.");
            Assert.IsTrue(root.TryGetProperty("error", out JsonElement _));
        }

        /// <remarks>
        /// A prompt which could not be built is answered as an error of the protocol whose message states the reason,
        /// exactly like a failed read of a resource and for the same reason: what a client receives must never be the
        /// reason of a failure in the place of the text it asked for.
        /// </remarks>
        [TestMethod]
        [TestProperty(nameof(GRYLibrary.Core.Misc.TestKind), nameof(GRYLibrary.Core.Misc.TestKind.IntegrationTest))]
        public void APromptWhichFailsAnswersWithTheReasonAsAnErrorOfTheProtocol()
        {
            // arrange
            using IntegrationTestFramework framework = new IntegrationTestFramework();
            // The script of the prompt is removed, which is a failure of the program behind the server and which it
            // reports with a readable reason.
            File.Delete(Path.Combine(framework.ExampleData.Folder, ExampleDataCreator.ExamplePromptScriptFileName));

            // act
            (HttpStatusCode statusCode, string answer) = framework.SendMCPMessageAndGetStatusCode(@"{""jsonrpc"":""2.0"",""id"":4,""method"":""prompts/get"",""params"":{""name"":""" + ExampleDataCreator.ExamplePromptName + @""",""arguments"":{""" + ExampleDataCreator.ExamplePromptArgumentName + @""":""cryptography""}}}");

            // assert
            Assert.AreEqual(HttpStatusCode.OK, statusCode);
            JsonElement root = JsonDocument.Parse(IntegrationTestFramework.GetPayloadOfAnswer(answer)).RootElement;
            Assert.IsFalse(root.TryGetProperty("result", out JsonElement _), "A prompt which could not be built must not be answered with a text.");
            string message = root.GetProperty("error").GetProperty("message").ToString();
            StringAssert.Contains(message, ExampleDataCreator.ExamplePromptName);
            StringAssert.Contains(message, "does not exist");
        }

        /// <remarks>
        /// A read which did not work is answered as an error of the protocol whose message states the reason, so that
        /// the caller learns why it did not receive the content. The http-answer carries the status-code of a normal
        /// answer, unlike a failed tool-call: a failed tool-call is a successful answer of the protocol and needs the
        /// status-code 502 to be visible as a failure, while an error of the protocol is unmistakable on its own.
        /// </remarks>
        [TestMethod]
        [TestProperty(nameof(GRYLibrary.Core.Misc.TestKind), nameof(GRYLibrary.Core.Misc.TestKind.IntegrationTest))]
        public void AReadWhichFailsAnswersWithTheReasonAsAnErrorOfTheProtocol()
        {
            // arrange
            using IntegrationTestFramework framework = new IntegrationTestFramework();
            // The script of the resource is removed, which is a failure of the program behind the server and which it
            // reports with a readable reason.
            File.Delete(Path.Combine(framework.ExampleData.Folder, ExampleDataCreator.ExampleResourceScriptFileName));

            // act
            (HttpStatusCode statusCode, string answer) = framework.SendMCPMessageAndGetStatusCode(@"{""jsonrpc"":""2.0"",""id"":4,""method"":""resources/read"",""params"":{""uri"":""" + ExampleDataCreator.ExampleResourceUri + @"""}}");

            // assert
            Assert.AreEqual(HttpStatusCode.OK, statusCode);
            JsonElement root = JsonDocument.Parse(IntegrationTestFramework.GetPayloadOfAnswer(answer)).RootElement;
            Assert.IsFalse(root.TryGetProperty("result", out JsonElement _), "A read which did not work must not be answered with a content.");
            string message = root.GetProperty("error").GetProperty("message").ToString();
            StringAssert.Contains(message, ExampleDataCreator.ExampleResourceName);
            StringAssert.Contains(message, "does not exist");
        }

        /// <remarks>
        /// An mcp-server which declares a completion claims the completions-capability of the protocol, and one which
        /// declares none does not: a client reads the capabilities of the handshake to decide whether it asks for the
        /// possible values of an argument at all.
        /// </remarks>
        [TestMethod]
        [TestProperty(nameof(GRYLibrary.Core.Misc.TestKind), nameof(GRYLibrary.Core.Misc.TestKind.IntegrationTest))]
        public void OnlyAnMCPServerWhichDeclaresACompletionClaimsTheCompletionsCapability()
        {
            // arrange
            using IntegrationTestFramework framework = new IntegrationTestFramework();
            string initialize = @"{""jsonrpc"":""2.0"",""id"":1,""method"":""initialize"",""params"":{""protocolVersion"":""2025-06-18"",""capabilities"":{},""clientInfo"":{""name"":""testcase"",""version"":""1.0.0""}}}";

            // act
            (HttpStatusCode _, string withACompletion) = framework.SendMCPMessageAndGetStatusCode(initialize, ExampleDataCreator.UrlNamespace, framework.AccessToken);
            (HttpStatusCode _, string withoutACompletion) = framework.SendMCPMessageAndGetStatusCode(initialize, ExampleDataCreator.AnonymousUrlNamespace, null);

            // assert
            JsonElement capabilitiesWithACompletion = JsonDocument.Parse(IntegrationTestFramework.GetPayloadOfAnswer(withACompletion)).RootElement.GetProperty("result").GetProperty("capabilities");
            JsonElement capabilitiesWithoutACompletion = JsonDocument.Parse(IntegrationTestFramework.GetPayloadOfAnswer(withoutACompletion)).RootElement.GetProperty("result").GetProperty("capabilities");
            Assert.IsTrue(capabilitiesWithACompletion.TryGetProperty("completions", out JsonElement _), "The mcp-server which declares a completion has to claim that capability.");
            Assert.IsFalse(capabilitiesWithoutACompletion.TryGetProperty("completions", out JsonElement _), "An mcp-server without a completion must not claim a capability which nothing of it answers.");
        }

        /// <remarks>
        /// Asking which values an argument of a prompt can have hands over the lines which the declared script wrote
        /// for the value which was entered so far, together with how many of them there are.
        /// </remarks>
        [TestMethod]
        [TestProperty(nameof(GRYLibrary.Core.Misc.TestKind), nameof(GRYLibrary.Core.Misc.TestKind.IntegrationTest))]
        public void AskingForTheValuesOfAnArgumentOverHTTPAnswersWithWhatItsScriptWrote()
        {
            // arrange
            using IntegrationTestFramework framework = new IntegrationTestFramework();

            // act
            (HttpStatusCode statusCode, string answer) = framework.SendMCPMessageAndGetStatusCode(@"{""jsonrpc"":""2.0"",""id"":1,""method"":""completion/complete"",""params"":{""ref"":{""type"":""ref/prompt"",""name"":""" + ExampleDataCreator.ExamplePromptName + @"""},""argument"":{""name"":""" + ExampleDataCreator.ExamplePromptArgumentName + @""",""value"":""c""}}}");

            // assert
            Assert.AreEqual(HttpStatusCode.OK, statusCode);
            JsonElement completion = JsonDocument.Parse(IntegrationTestFramework.GetPayloadOfAnswer(answer)).RootElement.GetProperty("result").GetProperty("completion");
            JsonElement values = completion.GetProperty("values");
            Assert.AreEqual(2, values.GetArrayLength(), "Exactly the values which fit the value which was entered so far have to reach the client.");
            Assert.AreEqual("cryptography", values[0].GetString());
            Assert.AreEqual("compilers", values[1].GetString());
            Assert.AreEqual(2, completion.GetProperty("total").GetInt32());
            Assert.IsFalse(completion.GetProperty("hasMore").GetBoolean());
        }

        /// <remarks>
        /// An argument which declares no completion and a reference which names no prompt are answered with no value
        /// and not with a failure: "which values are possible" is a question a client may ask about anything, and
        /// "none are known here" is an answer to it. A reference to a resource is such a case by definition, because
        /// this server declares every resource with the uri it has and derives none from a template.
        /// </remarks>
        [TestMethod]
        [TestProperty(nameof(GRYLibrary.Core.Misc.TestKind), nameof(GRYLibrary.Core.Misc.TestKind.IntegrationTest))]
        public void AskingForTheValuesOfSomethingWithoutACompletionAnswersWithNoValue()
        {
            // arrange
            using IntegrationTestFramework framework = new IntegrationTestFramework();

            // act
            (HttpStatusCode _, string ofAnotherArgument) = framework.SendMCPMessageAndGetStatusCode(@"{""jsonrpc"":""2.0"",""id"":1,""method"":""completion/complete"",""params"":{""ref"":{""type"":""ref/prompt"",""name"":""" + ExampleDataCreator.ExamplePromptName + @"""},""argument"":{""name"":""another_argument"",""value"":""""}}}");
            (HttpStatusCode _, string ofAResource) = framework.SendMCPMessageAndGetStatusCode(@"{""jsonrpc"":""2.0"",""id"":2,""method"":""completion/complete"",""params"":{""ref"":{""type"":""ref/resource"",""uri"":""" + ExampleDataCreator.ExampleResourceUri + @"""},""argument"":{""name"":""" + ExampleDataCreator.ExamplePromptArgumentName + @""",""value"":""""}}}");

            // assert
            foreach (string answer in new string[] { ofAnotherArgument, ofAResource })
            {
                JsonElement completion = JsonDocument.Parse(IntegrationTestFramework.GetPayloadOfAnswer(answer)).RootElement.GetProperty("result").GetProperty("completion");
                Assert.AreEqual(0, completion.GetProperty("values").GetArrayLength());
                Assert.AreEqual(0, completion.GetProperty("total").GetInt32());
            }
        }

        /// <remarks>
        /// An mcp-server which declares no completion answers nothing at all here, exactly like one without resources
        /// and one without prompts: it claims the capability not at all, so a client of it never asks for values which
        /// are empty by definition.
        /// </remarks>
        [TestMethod]
        [TestProperty(nameof(GRYLibrary.Core.Misc.TestKind), nameof(GRYLibrary.Core.Misc.TestKind.IntegrationTest))]
        public void AnMCPServerWhichDeclaresNoCompletionOffersNoCompletionsAtAll()
        {
            // arrange
            using IntegrationTestFramework framework = new IntegrationTestFramework();

            // act: the anonymous mcp-server of the example-data declares a tool and no prompt, so it declares no completion either
            (HttpStatusCode _, string answer) = framework.SendMCPMessageAndGetStatusCode(@"{""jsonrpc"":""2.0"",""id"":1,""method"":""completion/complete"",""params"":{""ref"":{""type"":""ref/prompt"",""name"":""" + ExampleDataCreator.ExamplePromptName + @"""},""argument"":{""name"":""topic"",""value"":""""}}}", ExampleDataCreator.AnonymousUrlNamespace, null);

            // assert
            JsonElement root = JsonDocument.Parse(IntegrationTestFramework.GetPayloadOfAnswer(answer)).RootElement;
            Assert.IsFalse(root.TryGetProperty("result", out JsonElement _), "An mcp-server without completions must not answer values.");
            Assert.IsTrue(root.TryGetProperty("error", out JsonElement _));
        }

        /// <remarks>
        /// A completion which did not work is answered as an error of the protocol whose message states the reason,
        /// exactly like a failed read of a resource and a prompt which could not be built: a client which asked which
        /// values are possible must not receive the reason of a failure as if it were one of them.
        /// </remarks>
        [TestMethod]
        [TestProperty(nameof(GRYLibrary.Core.Misc.TestKind), nameof(GRYLibrary.Core.Misc.TestKind.IntegrationTest))]
        public void ACompletionWhichFailsAnswersWithTheReasonAsAnErrorOfTheProtocol()
        {
            // arrange
            using IntegrationTestFramework framework = new IntegrationTestFramework();
            // The script of the completion is removed, which is a failure of the program behind the server and which it
            // reports with a readable reason.
            File.Delete(Path.Combine(framework.ExampleData.Folder, ExampleDataCreator.ExampleCompletionScriptFileName));

            // act
            (HttpStatusCode statusCode, string answer) = framework.SendMCPMessageAndGetStatusCode(@"{""jsonrpc"":""2.0"",""id"":1,""method"":""completion/complete"",""params"":{""ref"":{""type"":""ref/prompt"",""name"":""" + ExampleDataCreator.ExamplePromptName + @"""},""argument"":{""name"":""" + ExampleDataCreator.ExamplePromptArgumentName + @""",""value"":""""}}}");

            // assert
            Assert.AreEqual(HttpStatusCode.OK, statusCode);
            JsonElement root = JsonDocument.Parse(IntegrationTestFramework.GetPayloadOfAnswer(answer)).RootElement;
            Assert.IsFalse(root.TryGetProperty("result", out JsonElement _), "A completion which did not work must not be answered with values.");
            string message = root.GetProperty("error").GetProperty("message").ToString();
            StringAssert.Contains(message, ExampleDataCreator.ExamplePromptName);
            StringAssert.Contains(message, "does not exist");
        }

        /// <summary>
        /// The message which calls the tool that waits, with the folder it reports into as its value.
        /// </summary>
        private static string CreateCallOfTheWaitingTool(int requestId, WaitingScript script)
        {
            return @"{""jsonrpc"":""2.0"",""id"":" + requestId + @",""method"":""tools/call"",""params"":{""name"":""" + ExampleDataCreator.WaitingExampleToolName + @""",""arguments"":{""" + ExampleDataCreator.ExampleToolParameterName + @""":" + JsonSerializer.Serialize(script.ReportFolder) + @"}}}";
        }

        /// <summary>
        /// The message with which a client cancels the request of the given id.
        /// </summary>
        private static string CreateCancellationOf(int requestId)
        {
            return @"{""jsonrpc"":""2.0"",""method"":""notifications/cancelled"",""params"":{""requestId"":" + requestId + @",""reason"":""the client does not need the answer anymore""}}";
        }

        /// <remarks>
        /// The call and its cancellation are two http-requests which know nothing of each other, because the transport
        /// runs in the stateless mode. That the second one reaches the program which the first one started is the whole
        /// point of this, and the canary of the script is what states it: the answer of the call would only state that
        /// nobody waits for it anymore.
        /// </remarks>
        [TestMethod]
        [TestProperty(nameof(GRYLibrary.Core.Misc.TestKind), nameof(GRYLibrary.Core.Misc.TestKind.IntegrationTest))]
        public void ACancelledToolCallIsStoppedOverHTTP()
        {
            // arrange
            using IntegrationTestFramework framework = new IntegrationTestFramework();
            using WaitingScript script = new WaitingScript();
            Task<(HttpStatusCode StatusCode, string Answer)> call = Task.Run(() => framework.SendMCPMessageAndGetStatusCode(CreateCallOfTheWaitingTool(1, script)));
            script.WaitUntilItRuns();

            // act
            (HttpStatusCode statusCodeOfTheCancellation, string _) = framework.SendMCPMessageAndGetStatusCode(CreateCancellationOf(1));

            // assert
            Assert.IsTrue(Enumerable.Range(200, 100).Contains((int)statusCodeOfTheCancellation), $"A notification is answered with nothing but a success; it was answered with {(int)statusCodeOfTheCancellation}.");
            (HttpStatusCode statusCodeOfTheCall, string answer) = call.GetAwaiter().GetResult();
            Assert.AreEqual(HttpStatusCode.OK, statusCodeOfTheCall, "A call which the client itself cancelled is no failure of the program behind this server, so it does not carry the status-code of one.");
            JsonElement result = JsonDocument.Parse(IntegrationTestFramework.GetPayloadOfAnswer(answer)).RootElement.GetProperty("result");
            Assert.IsTrue(result.GetProperty("isError").GetBoolean(), "The client asked for a result and gets none, which it learns from the answer.");
            StringAssert.Contains(result.GetProperty("content")[0].GetProperty("text").GetString(), "cancelled");
            Assert.IsFalse(script.ItStillRunsToItsEndAfterItWasReleased(), "The script has to be gone: a script which is still running writes its canary as soon as it is released.");
        }

        /// <remarks>
        /// A request-id is only unambiguous within the client which chose it and says nothing about the mcp-server it
        /// was sent to, so a cancellation may only stop a call of the same mcp-server and of the same client. Which
        /// those are follows from the request the notification arrived with and not from the notification itself.
        /// </remarks>
        [TestMethod]
        [TestProperty(nameof(GRYLibrary.Core.Misc.TestKind), nameof(GRYLibrary.Core.Misc.TestKind.IntegrationTest))]
        public void ACancellationAtAnotherMCPServerDoesNotStopTheCallOverHTTP()
        {
            // arrange
            using IntegrationTestFramework framework = new IntegrationTestFramework();
            using WaitingScript script = new WaitingScript();
            Task<(HttpStatusCode StatusCode, string Answer)> call = Task.Run(() => framework.SendMCPMessageAndGetStatusCode(CreateCallOfTheWaitingTool(1, script)));
            script.WaitUntilItRuns();

            // act: the same request-id, but sent to the mcp-server which everybody may use and therefore by another caller
            framework.SendMCPMessageAndGetStatusCode(CreateCancellationOf(1), ExampleDataCreator.AnonymousUrlNamespace, null);

            // assert
            Assert.IsTrue(script.ItStillRunsToItsEndAfterItWasReleased(), "The call has to be untouched, so the script runs to its end as soon as it is released.");
            (HttpStatusCode statusCodeOfTheCall, string answer) = call.GetAwaiter().GetResult();
            Assert.AreEqual(HttpStatusCode.OK, statusCodeOfTheCall);
            JsonElement result = JsonDocument.Parse(IntegrationTestFramework.GetPayloadOfAnswer(answer)).RootElement.GetProperty("result");
            Assert.IsFalse(result.GetProperty("isError").GetBoolean());
            Assert.AreEqual(WaitingScript.AnswerOfTheScript, result.GetProperty("content")[0].GetProperty("text").GetString());
        }

        /// <remarks>
        /// A client cancels a call it does not have an answer for yet, so a cancellation which names a call that already
        /// ended is the normal case of one which arrives too late. It is answered like every other notification: with
        /// nothing at all and above all not with an error.
        /// </remarks>
        [TestMethod]
        [TestProperty(nameof(GRYLibrary.Core.Misc.TestKind), nameof(GRYLibrary.Core.Misc.TestKind.IntegrationTest))]
        public void ACancellationWhichNamesNoRunningCallIsNoFailureOverHTTP()
        {
            // arrange
            using IntegrationTestFramework framework = new IntegrationTestFramework();

            // act
            (HttpStatusCode statusCode, string answer) = framework.SendMCPMessageAndGetStatusCode(CreateCancellationOf(42));

            // assert
            Assert.IsTrue(Enumerable.Range(200, 100).Contains((int)statusCode), $"The notification was answered with {(int)statusCode}.");
            Assert.IsFalse(answer.Contains("error", System.StringComparison.OrdinalIgnoreCase), $"A cancellation which arrives too late must not be answered with an error. The answer is: {answer}");
        }
    }
}
