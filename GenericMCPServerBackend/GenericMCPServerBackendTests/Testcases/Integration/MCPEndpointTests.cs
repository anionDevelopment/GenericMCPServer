using GenericMCPServerBackend.Tests.TestUtilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;
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
            Assert.AreEqual(1, tools.GetArrayLength(), "The server has to offer exactly the tools which the example-data declares.");
            JsonElement tool = tools[0];
            Assert.AreEqual(ExampleDataCreator.ExampleToolName, tool.GetProperty("name").GetString());
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
    }
}
