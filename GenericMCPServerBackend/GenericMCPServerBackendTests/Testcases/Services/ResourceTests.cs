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
    /// Covers the resources: content which a client reads by a uri, declared by an administrator exactly like a tool.
    /// </summary>
    /// <remarks>
    /// Every testcase brings its own script and its own declaration in a temporary folder, for the same reason as the
    /// testcases of the tools do: the codeunit knows no resource, so a testcase which used one of the examples of the
    /// repository would test the example instead of the mechanism.
    /// </remarks>
    [TestClass]
    public class ResourceTests
    {
        private const string UrlNamespace = MCPServerWrapper.UrlNamespace;

        private const string UriOfTheResource = "https://example.com/handbook";

        /// <summary>A script which answers with the uri it was called for, so that a testcase can see what it got.</summary>
        private const string ScriptContent = @"import argparse
import sys

parser = argparse.ArgumentParser()
parser.add_argument('--uri', required=True)
arguments = parser.parse_args()
sys.stdout.write('the content of ' + arguments.uri)
";

        /// <summary>A script which fails with a reason, so that a testcase can see what reaches the caller.</summary>
        private const string FailingScriptContent = @"import sys

sys.stderr.write('the content is not available')
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

        private const string OneResource = @"{
            ""resources"": [
                {
                    ""uri"": """ + UriOfTheResource + @""",
                    ""resource-name"": ""handbook"",
                    ""type"": ""python"",
                    ""calls"": ""ReadResource.py"",
                    ""description"": ""The handbook of the team."",
                    ""mime-type"": ""text/markdown""
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
            File.WriteAllText(Path.Combine(folder, "ReadResource.py"), ScriptContent);
            File.WriteAllText(Path.Combine(folder, "FailingResource.py"), FailingScriptContent);
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
        public void ADeclaredResourceIsOfferedWithEverythingItWasDeclaredWith()
        {
            // arrange
            string folder = CreateFolder();
            try
            {
                // act
                IList<ResourceDefinition> definitions = CreateBusinessLogicService(folder, OneResource).GetMCPServers()[0].Resources;

                // assert
                Assert.AreEqual(1, definitions.Count);
                Assert.AreEqual(UriOfTheResource, definitions[0].Uri);
                Assert.AreEqual("handbook", definitions[0].ResourceName);
                Assert.AreEqual("text/markdown", definitions[0].MimeType);
                Assert.AreEqual("The handbook of the team.", definitions[0].Description);
                Assert.AreEqual(ToolTypes.Python, definitions[0].Type);
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }

        /// <remarks>
        /// The script receives the uri of the resource it is called for. That is the whole request: a client sends
        /// nothing else, and it is what lets one script implement several resources.
        /// </remarks>
        [TestMethod]
        [TestProperty(nameof(GRYLibrary.Core.Misc.TestKind), nameof(GRYLibrary.Core.Misc.TestKind.UnitTest))]
        public void ReadingAResourceAnswersWithWhatItsScriptWroteForThatUri()
        {
            // arrange
            string folder = CreateFolder();
            try
            {
                IBusinessLogicService businessLogicService = CreateBusinessLogicService(folder, OneResource);

                // act
                string content = businessLogicService.ReadResource(UrlNamespace, UriOfTheResource);

                // assert
                Assert.AreEqual($"the content of {UriOfTheResource}", content);
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }

        /// <remarks>
        /// Two resources of one script are the reason why the uri is handed over at all: the script decides with it
        /// what it answers.
        /// </remarks>
        [TestMethod]
        [TestProperty(nameof(GRYLibrary.Core.Misc.TestKind), nameof(GRYLibrary.Core.Misc.TestKind.UnitTest))]
        public void OneScriptCanImplementSeveralResources()
        {
            // arrange
            string folder = CreateFolder();
            try
            {
                string twoResources = @"{
                    ""resources"": [
                        {
                            ""uri"": ""https://example.com/first"",
                            ""resource-name"": ""first"",
                            ""type"": ""python"",
                            ""calls"": ""ReadResource.py"",
                            ""description"": ""The first document."",
                            ""mime-type"": ""text/plain""
                        },
                        {
                            ""uri"": ""https://example.com/second"",
                            ""resource-name"": ""second"",
                            ""type"": ""python"",
                            ""calls"": ""ReadResource.py"",
                            ""description"": ""The second document."",
                            ""mime-type"": ""text/plain""
                        }
                    ]
                }";
                IBusinessLogicService businessLogicService = CreateBusinessLogicService(folder, twoResources);

                // act
                string first = businessLogicService.ReadResource(UrlNamespace, "https://example.com/first");
                string second = businessLogicService.ReadResource(UrlNamespace, "https://example.com/second");

                // assert
                Assert.AreEqual("the content of https://example.com/first", first);
                Assert.AreEqual("the content of https://example.com/second", second);
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }

        [TestMethod]
        [TestProperty(nameof(GRYLibrary.Core.Misc.TestKind), nameof(GRYLibrary.Core.Misc.TestKind.UnitTest))]
        public void AResourceWhichIsNotDeclaredIsUnknown()
        {
            // arrange
            string folder = CreateFolder();
            try
            {
                IBusinessLogicService businessLogicService = CreateBusinessLogicService(folder, OneResource);

                // act & assert
                Assert.ThrowsException<KeyNotFoundException>(() => businessLogicService.ReadResource(UrlNamespace, "https://example.com/something-else"));
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }

        /// <remarks>
        /// What a script reported is what a caller needs to learn why it did not receive the content.
        /// </remarks>
        [TestMethod]
        [TestProperty(nameof(GRYLibrary.Core.Misc.TestKind), nameof(GRYLibrary.Core.Misc.TestKind.UnitTest))]
        public void AResourceWhoseScriptFailsReportsTheReasonOfTheScript()
        {
            // arrange
            string folder = CreateFolder();
            try
            {
                string failingResource = @"{
                    ""resources"": [
                        {
                            ""uri"": """ + UriOfTheResource + @""",
                            ""resource-name"": ""failing_resource"",
                            ""type"": ""python"",
                            ""calls"": ""FailingResource.py"",
                            ""description"": ""A document which can not be read."",
                            ""mime-type"": ""text/plain""
                        }
                    ]
                }";
                IBusinessLogicService businessLogicService = CreateBusinessLogicService(folder, failingResource);

                // act
                InvalidOperationException exception = Assert.ThrowsException<InvalidOperationException>(() => businessLogicService.ReadResource(UrlNamespace, UriOfTheResource));

                // assert
                StringAssert.Contains(exception.Message, "the content is not available");
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }

        /// <remarks>
        /// The environments of all declarations of a server lie in one folder, so a resource and a tool of the same
        /// name would otherwise share one environment and rebuild it for each other as soon as their requirements
        /// differ.
        /// </remarks>
        [TestMethod]
        [TestProperty(nameof(GRYLibrary.Core.Misc.TestKind), nameof(GRYLibrary.Core.Misc.TestKind.UnitTest))]
        public void AResourceAndAToolOfTheSameNameUseTwoVirtualEnvironments()
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
                    ""resources"": [
                        {
                            ""uri"": """ + UriOfTheResource + @""",
                            ""resource-name"": ""interpreter"",
                            ""type"": ""python"",
                            ""calls"": ""Interpreter.py"",
                            ""description"": ""Answers with its interpreter."",
                            ""mime-type"": ""text/plain""
                        }
                    ]
                }";
                IBusinessLogicService businessLogicService = CreateBusinessLogicService(folder, sameName);

                // act
                string interpreterOfTheTool = businessLogicService.RunTool(UrlNamespace, "interpreter", new Dictionary<string, string>(), CancellationToken.None);
                string interpreterOfTheResource = businessLogicService.ReadResource(UrlNamespace, UriOfTheResource);

                // assert
                Assert.AreNotEqual(interpreterOfTheTool, interpreterOfTheResource, "A resource and a tool of the same name must not share one virtual environment.");
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }

        /// <remarks>
        /// A resource is read by its uri, so two resources with the same uri would make it undecidable which of them a
        /// client means.
        /// </remarks>
        [TestMethod]
        [TestProperty(nameof(GRYLibrary.Core.Misc.TestKind), nameof(GRYLibrary.Core.Misc.TestKind.UnitTest))]
        public void AUriWhichIsDeclaredTwiceInOneMCPServerIsAnError()
        {
            // arrange
            string folder = CreateFolder();
            try
            {
                string theSameUriTwice = @"{
                    ""resources"": [
                        {
                            ""uri"": """ + UriOfTheResource + @""",
                            ""resource-name"": ""first"",
                            ""type"": ""python"",
                            ""calls"": ""ReadResource.py"",
                            ""description"": ""x"",
                            ""mime-type"": ""text/plain""
                        },
                        {
                            ""uri"": """ + UriOfTheResource + @""",
                            ""resource-name"": ""second"",
                            ""type"": ""python"",
                            ""calls"": ""ReadResource.py"",
                            ""description"": ""x"",
                            ""mime-type"": ""text/plain""
                        }
                    ]
                }";
                string file = CreateDefinitions(folder, theSameUriTwice);

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
        /// read the affected resource.
        /// </remarks>
        [TestMethod]
        [TestProperty(nameof(GRYLibrary.Core.Misc.TestKind), nameof(GRYLibrary.Core.Misc.TestKind.UnitTest))]
        public void AnIncompleteDeclarationOfAResourceIsAnError()
        {
            // arrange
            string folder = CreateFolder();
            try
            {
                string withoutAUri = @"{ ""resources"": [ { ""resource-name"": ""handbook"", ""type"": ""python"", ""calls"": ""ReadResource.py"", ""description"": ""x"", ""mime-type"": ""text/plain"" } ] }";
                string withoutAMimeType = @"{ ""resources"": [ { ""uri"": """ + UriOfTheResource + @""", ""resource-name"": ""handbook"", ""type"": ""python"", ""calls"": ""ReadResource.py"", ""description"": ""x"" } ] }";
                string withoutAName = @"{ ""resources"": [ { ""uri"": """ + UriOfTheResource + @""", ""type"": ""python"", ""calls"": ""ReadResource.py"", ""description"": ""x"", ""mime-type"": ""text/plain"" } ] }";
                string withoutAType = @"{ ""resources"": [ { ""uri"": """ + UriOfTheResource + @""", ""resource-name"": ""handbook"", ""calls"": ""ReadResource.py"", ""description"": ""x"", ""mime-type"": ""text/plain"" } ] }";

                // act & assert
                foreach (string declaration in new string[] { withoutAUri, withoutAMimeType, withoutAName, withoutAType })
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
        /// A resource is read by its uri and nothing else, so a declared parameter would claim a possibility which
        /// does not exist.
        /// </remarks>
        [TestMethod]
        [TestProperty(nameof(GRYLibrary.Core.Misc.TestKind), nameof(GRYLibrary.Core.Misc.TestKind.UnitTest))]
        public void ParametersOfAResourceAreAnError()
        {
            // arrange
            string folder = CreateFolder();
            try
            {
                string withParameters = @"{ ""resources"": [ { ""uri"": """ + UriOfTheResource + @""", ""resource-name"": ""handbook"", ""type"": ""python"", ""calls"": ""ReadResource.py"", ""description"": ""x"", ""mime-type"": ""text/plain"", ""parameters"": [ ""value"" ] } ] }";
                string file = CreateDefinitions(folder, withParameters);

                // act & assert
                Assert.ThrowsException<InvalidOperationException>(() => new BusinessLogicService(file, _VirtualEnvironmentsFolder));
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }

        /// <remarks>
        /// A uri-template is a resource whose uri a client fills in. This server offers the resources which are
        /// declared, so such a value is rejected instead of being offered as something which nothing answers.
        /// </remarks>
        [TestMethod]
        [TestProperty(nameof(GRYLibrary.Core.Misc.TestKind), nameof(GRYLibrary.Core.Misc.TestKind.UnitTest))]
        public void AUriWithATemplateExpressionIsAnError()
        {
            // arrange
            string folder = CreateFolder();
            try
            {
                string withATemplate = @"{ ""resources"": [ { ""uri"": ""https://example.com/{document}"", ""resource-name"": ""handbook"", ""type"": ""python"", ""calls"": ""ReadResource.py"", ""description"": ""x"", ""mime-type"": ""text/plain"" } ] }";
                string file = CreateDefinitions(folder, withATemplate);

                // act & assert
                Assert.ThrowsException<InvalidOperationException>(() => new BusinessLogicService(file, _VirtualEnvironmentsFolder));
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }

        /// <remarks>
        /// Such a resource is requested from its own uri, so there is no address which a client could ask for and
        /// therefore nothing which rules could allow or deny.
        /// </remarks>
        [TestMethod]
        [TestProperty(nameof(GRYLibrary.Core.Misc.TestKind), nameof(GRYLibrary.Core.Misc.TestKind.UnitTest))]
        public void AllowedUrlsOfAResourceAreAnError()
        {
            // arrange
            string folder = CreateFolder();
            try
            {
                string withAllowedUrls = @"{ ""resources"": [ { ""uri"": """ + UriOfTheResource + @""", ""resource-name"": ""handbook"", ""type"": ""web-request"", ""description"": ""x"", ""mime-type"": ""text/plain"", ""allowed-urls"": [ ""example\\.com/.*"" ] } ] }";
                string file = CreateDefinitions(folder, withAllowedUrls);

                // act & assert
                Assert.ThrowsException<InvalidOperationException>(() => new BusinessLogicService(file, _VirtualEnvironmentsFolder));
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }

        /// <remarks>
        /// The uri of such a resource is the address which is requested, and the protocol of a request of this server
        /// is always https.
        /// </remarks>
        [TestMethod]
        [TestProperty(nameof(GRYLibrary.Core.Misc.TestKind), nameof(GRYLibrary.Core.Misc.TestKind.UnitTest))]
        public void AWebRequestResourceWhoseUriStatesAnotherProtocolIsAnError()
        {
            // arrange
            string folder = CreateFolder();
            try
            {
                string withAnotherProtocol = @"{ ""resources"": [ { ""uri"": ""file:///documents/handbook.md"", ""resource-name"": ""handbook"", ""type"": ""web-request"", ""description"": ""x"", ""mime-type"": ""text/plain"" } ] }";
                string file = CreateDefinitions(folder, withAnotherProtocol);

                // act
                InvalidOperationException exception = Assert.ThrowsException<InvalidOperationException>(() => new BusinessLogicService(file, _VirtualEnvironmentsFolder));

                // assert
                StringAssert.Contains(exception.Message, WebRequestService.Protocol);
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }

        /// <remarks>
        /// Such a resource requests an address instead of running a script, exactly like a tool of that type does.
        /// </remarks>
        [TestMethod]
        [TestProperty(nameof(GRYLibrary.Core.Misc.TestKind), nameof(GRYLibrary.Core.Misc.TestKind.UnitTest))]
        public void AWebRequestResourceWhichStatesAScriptIsAnError()
        {
            // arrange
            string folder = CreateFolder();
            try
            {
                string withAScript = @"{ ""resources"": [ { ""uri"": """ + UriOfTheResource + @""", ""resource-name"": ""handbook"", ""type"": ""web-request"", ""calls"": ""ReadResource.py"", ""description"": ""x"", ""mime-type"": ""text/plain"" } ] }";
                string file = CreateDefinitions(folder, withAScript);

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
