using GenericMCPServerBackend.Tests.TestUtilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.Json;

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
            Assert.AreEqual(1, tools.GetArrayLength(), "The mcp-server has to offer exactly the tools which the example-data declares for it.");
            JsonElement tool = tools.EnumerateArray().Single(entry => entry.GetProperty("name").GetString() == ExampleDataCreator.ExampleToolName);
            Assert.AreEqual("Answers with the value it got.", tool.GetProperty("description").GetString());
            Assert.IsTrue(tool.GetProperty("inputSchema").GetProperty("properties").TryGetProperty(ExampleDataCreator.ExampleToolParameterName, out JsonElement _), "The declared parameter has to be part of the schema which the client gets.");
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
    }
}
