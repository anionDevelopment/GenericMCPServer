using GenericMCPServerBackend.Core.Model;
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
    /// Covers the tools which request an address instead of running a script.
    /// </summary>
    /// <remarks>
    /// The testcases exercise the decision which address a tool may request and never send a request which is allowed:
    /// a testcase which reached a foreign server would depend on that server and would say nothing about the decision
    /// which is the interesting part here.
    /// </remarks>
    [TestClass]
    public class WebRequestToolTests
    {
        private const string UrlNamespace = MCPServerWrapper.UrlNamespace;

        private const string OneTool = @"{
            ""tools"": [
                {
                    ""tool-name"": ""example_page"",
                    ""type"": ""web-request"",
                    ""description"": ""Requests a page of example.com."",
                    ""allowed-urls"": [ ""example\\.com/.*"" ]
                }
            ]
        }";

        private static string CreateFolder()
        {
            return Path.Combine(Path.GetTempPath(), "GenericMCPServerBackendTests", Guid.NewGuid().ToString());
        }

        private static IBusinessLogicService CreateBusinessLogicService(string folder, string toolDefinitions)
        {
            Directory.CreateDirectory(folder);
            string file = Path.Combine(folder, "Tools.json");
            File.WriteAllText(file, MCPServerWrapper.WrapInAnMCPServer(toolDefinitions));
            return new BusinessLogicService(file, Path.Combine(folder, "VirtualEnvironments"));
        }

        /// <remarks>
        /// Such a tool takes exactly the parameter which the mechanism defines, because what it does is decided by the
        /// address the client states and by the rules of the declaration.
        /// </remarks>
        [TestMethod]
        [TestProperty(nameof(GRYLibrary.Core.Misc.TestKind), nameof(GRYLibrary.Core.Misc.TestKind.UnitTest))]
        public void AWebRequestToolOffersTheAddressAsItsOnlyParameter()
        {
            // arrange
            string folder = CreateFolder();
            try
            {
                // act
                IList<ToolDefinition> definitions = CreateBusinessLogicService(folder, OneTool).GetMCPServers()[0].Tools;

                // assert
                CollectionAssert.AreEqual(new string[] { WebRequestService.UrlParameterName }, new List<string>(definitions[0].Parameters));
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }

        /// <remarks>
        /// Everything which no rule allows is denied. That direction is the one which a mistake in a declaration may
        /// take: a tool which can reach nothing is useless, a tool which can reach everything is dangerous.
        /// </remarks>
        [TestMethod]
        [TestProperty(nameof(GRYLibrary.Core.Misc.TestKind), nameof(GRYLibrary.Core.Misc.TestKind.UnitTest))]
        public void AnAddressWhichNoRuleAllowsIsDenied()
        {
            // arrange
            string folder = CreateFolder();
            try
            {
                IBusinessLogicService businessLogicService = CreateBusinessLogicService(folder, OneTool);

                // act
                InvalidOperationException exception = Assert.ThrowsException<InvalidOperationException>(() => businessLogicService.RunTool(UrlNamespace, "example_page", new Dictionary<string, string>() { { WebRequestService.UrlParameterName, "other.com/something" } }, CancellationToken.None));

                // assert
                StringAssert.Contains(exception.Message, "does not allow");
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }

        /// <remarks>
        /// A rule has to match the whole address. Without that, an address of a foreign host which contains an allowed
        /// address somewhere - in a parameter, in a path or in front of an "@" - would satisfy the rule.
        /// </remarks>
        [TestMethod]
        [TestProperty(nameof(GRYLibrary.Core.Misc.TestKind), nameof(GRYLibrary.Core.Misc.TestKind.UnitTest))]
        public void AnAddressWhichOnlyContainsAnAllowedAddressIsDenied()
        {
            // arrange
            string folder = CreateFolder();
            try
            {
                IBusinessLogicService businessLogicService = CreateBusinessLogicService(folder, OneTool);

                // act & assert
                Assert.ThrowsException<InvalidOperationException>(() => businessLogicService.RunTool(UrlNamespace, "example_page", new Dictionary<string, string>() { { WebRequestService.UrlParameterName, "other.com/?x=example.com/page" } }, CancellationToken.None), "An address which contains an allowed one must not be allowed.");
                Assert.ThrowsException<InvalidOperationException>(() => businessLogicService.RunTool(UrlNamespace, "example_page", new Dictionary<string, string>() { { WebRequestService.UrlParameterName, "example.com.other.com/page" } }, CancellationToken.None), "A host which only starts with an allowed one must not be allowed.");
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }

        /// <remarks>
        /// The protocol is not part of an address here: it is always https. An address which states another one is
        /// therefore not silently corrected but rejected, so that nobody believes an unencrypted request was sent.
        /// </remarks>
        [TestMethod]
        [TestProperty(nameof(GRYLibrary.Core.Misc.TestKind), nameof(GRYLibrary.Core.Misc.TestKind.UnitTest))]
        public void AnAddressOfAnotherProtocolIsRejected()
        {
            // arrange
            string folder = CreateFolder();
            try
            {
                IBusinessLogicService businessLogicService = CreateBusinessLogicService(folder, OneTool);

                // act
                InvalidOperationException exception = Assert.ThrowsException<InvalidOperationException>(() => businessLogicService.RunTool(UrlNamespace, "example_page", new Dictionary<string, string>() { { WebRequestService.UrlParameterName, "http://example.com/page" } }, CancellationToken.None));

                // assert
                StringAssert.Contains(exception.Message, WebRequestService.Protocol);
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }

        /// <remarks>
        /// The rules see the host of the parsed address, so credentials in front of it would be sent without ever
        /// having been checked. Such an address is therefore rejected instead of being cleaned up silently.
        /// </remarks>
        [TestMethod]
        [TestProperty(nameof(GRYLibrary.Core.Misc.TestKind), nameof(GRYLibrary.Core.Misc.TestKind.UnitTest))]
        public void AnAddressWithCredentialsIsRejected()
        {
            // arrange
            string folder = CreateFolder();
            try
            {
                IBusinessLogicService businessLogicService = CreateBusinessLogicService(folder, OneTool);

                // act & assert
                Assert.ThrowsException<InvalidOperationException>(() => businessLogicService.RunTool(UrlNamespace, "example_page", new Dictionary<string, string>() { { WebRequestService.UrlParameterName, "example.com@other.com/page" } }, CancellationToken.None));
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }

        [TestMethod]
        [TestProperty(nameof(GRYLibrary.Core.Misc.TestKind), nameof(GRYLibrary.Core.Misc.TestKind.UnitTest))]
        public void AWebRequestToolWithoutARuleIsAnError()
        {
            // arrange
            string folder = CreateFolder();
            try
            {
                string withoutARule = @"{ ""tools"": [ { ""tool-name"": ""example_page"", ""type"": ""web-request"", ""description"": ""x"", ""allowed-urls"": [ ] } ] }";

                // act & assert
                Assert.ThrowsException<InvalidOperationException>(() => CreateBusinessLogicService(folder, withoutARule));
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }

        /// <remarks>
        /// Such a tool runs no script, so a declared script would be a misunderstanding of what the tool does and not
        /// something which may be ignored.
        /// </remarks>
        [TestMethod]
        [TestProperty(nameof(GRYLibrary.Core.Misc.TestKind), nameof(GRYLibrary.Core.Misc.TestKind.UnitTest))]
        public void AWebRequestToolWhichStatesAScriptIsAnError()
        {
            // arrange
            string folder = CreateFolder();
            try
            {
                string withAScript = @"{ ""tools"": [ { ""tool-name"": ""example_page"", ""type"": ""web-request"", ""calls"": ""Something.py"", ""description"": ""x"", ""allowed-urls"": [ "".*"" ] } ] }";

                // act & assert
                Assert.ThrowsException<InvalidOperationException>(() => CreateBusinessLogicService(folder, withAScript));
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }

        [TestMethod]
        [TestProperty(nameof(GRYLibrary.Core.Misc.TestKind), nameof(GRYLibrary.Core.Misc.TestKind.UnitTest))]
        public void ARuleWhichIsNoValidRegularExpressionIsAnError()
        {
            // arrange
            string folder = CreateFolder();
            try
            {
                string withABrokenRule = @"{ ""tools"": [ { ""tool-name"": ""example_page"", ""type"": ""web-request"", ""description"": ""x"", ""allowed-urls"": [ ""example\\.com/["" ] } ] }";

                // act & assert
                Assert.ThrowsException<InvalidOperationException>(() => CreateBusinessLogicService(folder, withABrokenRule));
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }
    }
}
