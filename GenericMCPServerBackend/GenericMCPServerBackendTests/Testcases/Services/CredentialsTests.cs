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
    /// Covers the credentials a tool needs to reach a foreign service.
    /// </summary>
    /// <remarks>
    /// A credential belongs to the administrator and never to the client, so the testcases set it the way the operator
    /// of the container does: as an environment-variable of the server-process. Every testcase uses its own names and
    /// removes them again, so that none of them depends on another.
    ///
    /// The virtual environments live in one folder of the test-class, because an environment belongs to a tool-name and
    /// would otherwise be built once per testcase.
    /// </remarks>
    [TestClass]
    public class CredentialsTests
    {
        private const string UrlNamespace = MCPServerWrapper.UrlNamespace;

        /// <summary>A script which answers with the credentials it can see.</summary>
        private const string ScriptContent = @"import os
import sys

sys.stdout.write(os.environ.get('TOKEN', '<none>') + '|' + os.environ.get('GENERICMCPSERVER_TEST_FOREIGN', '<none>'))
";

        /// <summary>A script which writes its credential into its error-output and fails.</summary>
        private const string LeakingScriptContent = @"import os
import sys

sys.stderr.write('the request failed with the token ' + os.environ.get('TOKEN', '<none>'))
sys.exit(1)
";

        private const string NameOfTheVariable = "GENERICMCPSERVER_TEST_TOKEN";

        private const string NameOfTheForeignVariable = "GENERICMCPSERVER_TEST_FOREIGN";

        private const string Value = "s3cr3t-value-of-the-token";

        private static readonly string _VirtualEnvironmentsFolder = CreateFolder();

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

        private static string CreateToolDefinitions(string folder, string toolDefinitions)
        {
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, "Credentials.py"), ScriptContent);
            File.WriteAllText(Path.Combine(folder, "Leaking.py"), LeakingScriptContent);
            string file = Path.Combine(folder, "Tools.json");
            File.WriteAllText(file, MCPServerWrapper.WrapInAnMCPServer(toolDefinitions));
            return file;
        }

        private static IBusinessLogicService CreateBusinessLogicService(string folder, string toolDefinitions)
        {
            return new BusinessLogicService(CreateToolDefinitions(folder, toolDefinitions), _VirtualEnvironmentsFolder);
        }

        /// <summary>The tool "credentials" declares the variable of this test under the name "TOKEN".</summary>
        private const string ToolWithACredential = @"{
            ""tools"": [
                {
                    ""tool-name"": ""credentials"",
                    ""type"": ""python"",
                    ""calls"": ""Credentials.py"",
                    ""description"": ""Answers with the credentials it can see."",
                    ""credentials"": { ""TOKEN"": ""GENERICMCPSERVER_TEST_TOKEN"" },
                    ""parameters"": [ ]
                }
            ]
        }";

        /// <remarks>
        /// The two names are stated separately so that the same script can be declared twice with a different access
        /// each time, so the script has to see the name of the declaration and not the one of the server.
        /// </remarks>
        [TestMethod]
        [TestProperty(nameof(GRYLibrary.Core.Misc.TestKind), nameof(GRYLibrary.Core.Misc.TestKind.UnitTest))]
        public void AToolReceivesItsCredentialUnderTheNameItWasDeclaredWith()
        {
            // arrange
            string folder = CreateFolder();
            Environment.SetEnvironmentVariable(NameOfTheVariable, Value);
            try
            {
                IBusinessLogicService businessLogicService = CreateBusinessLogicService(folder, ToolWithACredential);

                // act
                string answer = businessLogicService.RunTool(UrlNamespace, "credentials", new Dictionary<string, string>());

                // assert
                Assert.AreEqual($"{Value}|<none>", answer);
            }
            finally
            {
                Environment.SetEnvironmentVariable(NameOfTheVariable, null);
                Directory.Delete(folder, true);
            }
        }

        /// <remarks>
        /// A script inherits the environment of the server, so without removing them every tool could read every
        /// credential - including the one which belongs to a tool of a completely different origin.
        /// </remarks>
        [TestMethod]
        [TestProperty(nameof(GRYLibrary.Core.Misc.TestKind), nameof(GRYLibrary.Core.Misc.TestKind.UnitTest))]
        public void AToolDoesNotReceiveTheCredentialOfAnotherTool()
        {
            // arrange
            string folder = CreateFolder();
            Environment.SetEnvironmentVariable(NameOfTheVariable, Value);
            Environment.SetEnvironmentVariable(NameOfTheForeignVariable, "the-secret-of-the-other-tool");
            try
            {
                // the second tool is what makes the foreign variable a credential of somebody else
                IBusinessLogicService businessLogicService = CreateBusinessLogicService(folder, @"{
                    ""tools"": [
                        {
                            ""tool-name"": ""credentials"",
                            ""type"": ""python"",
                            ""calls"": ""Credentials.py"",
                            ""description"": ""Answers with the credentials it can see."",
                            ""credentials"": { ""TOKEN"": ""GENERICMCPSERVER_TEST_TOKEN"" },
                            ""parameters"": [ ]
                        },
                        {
                            ""tool-name"": ""other_tool"",
                            ""type"": ""python"",
                            ""calls"": ""Credentials.py"",
                            ""description"": ""Another tool with another access."",
                            ""credentials"": { ""TOKEN"": ""GENERICMCPSERVER_TEST_FOREIGN"" },
                            ""parameters"": [ ]
                        }
                    ]
                }");

                // act
                string answer = businessLogicService.RunTool(UrlNamespace, "credentials", new Dictionary<string, string>());

                // assert
                Assert.AreEqual($"{Value}|<none>", answer, "The variable which the credential of the other tool is read from must not be visible here.");
            }
            finally
            {
                Environment.SetEnvironmentVariable(NameOfTheVariable, null);
                Environment.SetEnvironmentVariable(NameOfTheForeignVariable, null);
                Directory.Delete(folder, true);
            }
        }

        /// <remarks>
        /// This is how a secret reaches a container without standing in the compose-file: docker-secrets are files, and
        /// the variable states which one.
        /// </remarks>
        [TestMethod]
        [TestProperty(nameof(GRYLibrary.Core.Misc.TestKind), nameof(GRYLibrary.Core.Misc.TestKind.UnitTest))]
        public void TheValueOfACredentialCanBeReadFromAFile()
        {
            // arrange
            string folder = CreateFolder();
            Directory.CreateDirectory(folder);
            string fileWithTheValue = Path.Combine(folder, "Secret.txt");
            // the line-break is the one an editor adds; it is not part of the secret
            File.WriteAllText(fileWithTheValue, Value + Environment.NewLine);
            Environment.SetEnvironmentVariable(NameOfTheVariable + CredentialsService.NameSuffixForAFile, fileWithTheValue);
            try
            {
                IBusinessLogicService businessLogicService = CreateBusinessLogicService(folder, ToolWithACredential);

                // act
                string answer = businessLogicService.RunTool(UrlNamespace, "credentials", new Dictionary<string, string>());

                // assert
                Assert.AreEqual($"{Value}|<none>", answer);
            }
            finally
            {
                Environment.SetEnvironmentVariable(NameOfTheVariable + CredentialsService.NameSuffixForAFile, null);
                Directory.Delete(folder, true);
            }
        }

        /// <remarks>
        /// A forgotten secret has to be visible while deploying and not when a model happens to call the affected tool,
        /// so the server does not start at all.
        /// </remarks>
        [TestMethod]
        [TestProperty(nameof(GRYLibrary.Core.Misc.TestKind), nameof(GRYLibrary.Core.Misc.TestKind.UnitTest))]
        public void AMissingCredentialKeepsTheServerFromStarting()
        {
            // arrange
            string folder = CreateFolder();
            Environment.SetEnvironmentVariable(NameOfTheVariable, null);
            try
            {
                // act
                InvalidOperationException exception = Assert.ThrowsException<InvalidOperationException>(() => CreateBusinessLogicService(folder, ToolWithACredential));

                // assert
                StringAssert.Contains(exception.Message, NameOfTheVariable, "The message has to name the credential which is missing.");
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }

        /// <remarks>
        /// The message of a failed tool-call goes to the client, so a script which wrote its credential into it would
        /// hand the credential to the model.
        /// </remarks>
        [TestMethod]
        [TestProperty(nameof(GRYLibrary.Core.Misc.TestKind), nameof(GRYLibrary.Core.Misc.TestKind.UnitTest))]
        public void TheValueOfACredentialIsRemovedFromAMessageWhichGoesToTheClient()
        {
            // arrange
            string folder = CreateFolder();
            Environment.SetEnvironmentVariable(NameOfTheVariable, Value);
            try
            {
                IBusinessLogicService businessLogicService = CreateBusinessLogicService(folder, @"{
                    ""tools"": [
                        {
                            ""tool-name"": ""leaking"",
                            ""type"": ""python"",
                            ""calls"": ""Leaking.py"",
                            ""description"": ""Writes its credential into its error-output."",
                            ""credentials"": { ""TOKEN"": ""GENERICMCPSERVER_TEST_TOKEN"" },
                            ""parameters"": [ ]
                        }
                    ]
                }");
                InvalidOperationException exception = Assert.ThrowsException<InvalidOperationException>(() => businessLogicService.RunTool(UrlNamespace, "leaking", new Dictionary<string, string>()));
                StringAssert.Contains(exception.Message, Value, "The script really has to have written its credential, otherwise this testcase would prove nothing.");

                // act: this is what ScriptTool does before the message becomes the result of the call
                string messageForTheClient = businessLogicService.RemoveCredentialValuesFrom(exception.Message);

                // assert
                Assert.IsFalse(messageForTheClient.Contains(Value, StringComparison.Ordinal), "The value must not reach the client.");
                StringAssert.Contains(messageForTheClient, "***");
            }
            finally
            {
                Environment.SetEnvironmentVariable(NameOfTheVariable, null);
                Directory.Delete(folder, true);
            }
        }

        [TestMethod]
        [TestProperty(nameof(GRYLibrary.Core.Misc.TestKind), nameof(GRYLibrary.Core.Misc.TestKind.UnitTest))]
        public void AHeaderReceivesTheValueOfTheCredentialItRefersTo()
        {
            // arrange
            ToolDefinition definition = new ToolDefinition()
            {
                ToolName = "example_page",
                Type = ToolTypes.WebRequest,
                Headers = new Dictionary<string, string>()
                {
                    { "Authorization", "Bearer ${TOKEN}" },
                    { "Accept", "application/json" },
                },
            };

            // act
            IDictionary<string, string> headers = WebRequestService.BuildHeaders(definition, new Dictionary<string, string>() { { "TOKEN", Value } });

            // assert
            Assert.AreEqual($"Bearer {Value}", headers["Authorization"]);
            Assert.AreEqual("application/json", headers["Accept"], "A header without a reference stays what it is.");
        }

        /// <remarks>
        /// A reference which points at nothing would become a request whose header is incomplete without anybody
        /// noticing, so it is a mistake of the declaration.
        /// </remarks>
        [TestMethod]
        [TestProperty(nameof(GRYLibrary.Core.Misc.TestKind), nameof(GRYLibrary.Core.Misc.TestKind.UnitTest))]
        public void AHeaderWhichRefersToACredentialWhichIsNotDeclaredIsAnError()
        {
            // arrange
            string folder = CreateFolder();
            try
            {
                string withAnUnknownReference = @"{ ""tools"": [ { ""tool-name"": ""example_page"", ""type"": ""web-request"", ""description"": ""x"", ""allowed-urls"": [ ""example\\.com/.*"" ], ""headers"": { ""Authorization"": ""Bearer ${TOKEN}"" } } ] }";

                // act & assert
                Assert.ThrowsException<InvalidOperationException>(() => CreateBusinessLogicService(folder, withAnUnknownReference));
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }

        /// <remarks>
        /// A field which does not belong to the type of a tool is a misunderstanding of what that tool does, so it is
        /// not ignored.
        /// </remarks>
        [TestMethod]
        [TestProperty(nameof(GRYLibrary.Core.Misc.TestKind), nameof(GRYLibrary.Core.Misc.TestKind.UnitTest))]
        public void HeadersOfAPythonToolAndCredentialsOfAShellToolAreAnError()
        {
            // arrange
            string folder = CreateFolder();
            try
            {
                string pythonToolWithHeaders = @"{ ""tools"": [ { ""tool-name"": ""credentials"", ""type"": ""python"", ""calls"": ""Credentials.py"", ""description"": ""x"", ""headers"": { ""Accept"": ""application/json"" } } ] }";
                string shellToolWithCredentials = @"{ ""tools"": [ { ""tool-name"": ""shell_tool"", ""type"": ""shell"", ""calls"": ""Credentials.py"", ""description"": ""x"", ""credentials"": { ""TOKEN"": ""GENERICMCPSERVER_TEST_TOKEN"" } } ] }";

                // act & assert
                Assert.ThrowsException<InvalidOperationException>(() => CreateBusinessLogicService(folder, pythonToolWithHeaders));
                Assert.ThrowsException<InvalidOperationException>(() => CreateBusinessLogicService(folder, shellToolWithCredentials));
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }
    }
}
