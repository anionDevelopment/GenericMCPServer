using GenericMCPServerBackend.Core;
using GenericMCPServerBackend.Tests.TestUtilities;
using GRYLibrary.Core.APIServer.Settings.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Net.Http;
using System.Threading;

namespace GenericMCPServerBackend.Tests.Testcases.Integration
{
    /// <summary>
    /// Exercises the values which the server writes into its configuration-file while it generates that file.
    /// </summary>
    /// <remarks>
    /// This needs a started server and no http: what is interesting is what the first start writes down, which is
    /// decided while the server comes up and not by any route it hosts afterwards.
    /// </remarks>
    [TestClass]
    public class InitialConfigurationTests
    {
        /// <summary>How long the testcase waits for the started server to be up.</summary>
        private static readonly TimeSpan _DurationUntilTheServerIsReady = TimeSpan.FromSeconds(120);

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
        /// This is what makes a declaration mountable as a single file: the stated value is the whole path of that
        /// file, so the declaration does not have to lie in the tools-folder - and the folder it does lie in does not
        /// have to be handed to the server along with it.
        /// </remarks>
        [TestMethod]
        [TestProperty(nameof(GRYLibrary.Core.Misc.TestKind), nameof(GRYLibrary.Core.Misc.TestKind.IntegrationTest))]
        public void TheWholePathOfTheDeclarationCanBeStatedWhileStarting()
        {
            // arrange: a folder which is none of the folders of the server, which is the case this is about
            string folderOfItsOwn = Path.Combine(Path.GetTempPath(), "GenericMCPServerBackendTests", Guid.NewGuid().ToString());
            Directory.CreateDirectory(folderOfItsOwn);
            string declaration = Path.Combine(folderOfItsOwn, "Declaration.json");
            string toolsFolder = string.Empty;
            Program program = new Program()
            {
                RunAsync = false,
                ListenOnEveryIP = true,
                // The tools-folder is not wiped between two test-runs, so what an earlier one left there is removed: a
                // "Tools.json" of an earlier run would make the assertion below fail for a reason which is not the one
                // it asks about. The folder itself is recreated right away, because the server expects the folder of
                // this run to exist.
                OnToolsFolderResolved = folder =>
                {
                    GRYLibrary.Core.Misc.Utilities.EnsureDirectoryDoesNotExist(folder);
                    GRYLibrary.Core.Misc.Utilities.EnsureDirectoryExists(folder);
                    toolsFolder = folder;
                },
            };

            // act
            Thread thread = new Thread(() => program.MainImplementation(new string[]
            {
                $"--{nameof(GenericMCPServerBackend.Core.Configuration.CommandlineParameter.InitialToolsDefinitionFilePath)}", declaration,
            }));
            thread.Start();
            try
            {
                // The server has to be up before anything is asked of it and before it is stopped again: it resolves
                // its declaration while it comes up, and stopping it in the middle of that leaves it half-started.
                if (!GRYLibrary.Core.Misc.Utilities.RunWithTimeout(() =>
                {
                    while (!ItIsUp())
                    {
                        Thread.Sleep(TimeSpan.FromMilliseconds(200));
                    }
                }, _DurationUntilTheServerIsReady))
                {
                    Assert.Fail("The test-server did not become ready.");
                }

                // assert: the server creates the declaration it resolved when there is none yet, so which file exists
                // is what states which file the server actually looked at.
                Assert.IsTrue(File.Exists(declaration), "The server has to use the stated path as it is.");
                Assert.IsFalse(File.Exists(Path.Combine(toolsFolder, "Tools.json")), "The default has to be replaced by the stated value and not to be used next to it.");
                Assert.IsTrue(TheConfigurationFileStates(toolsFolder, declaration), "The configuration-file has to state the whole path of the declaration, so that an administrator reads there where it is looked for.");
            }
            finally
            {
                if (program.IsRunning)
                {
                    program.Stop();
                }
                Directory.Delete(folderOfItsOwn, true);
            }
        }

        /// <summary>
        /// States whether the generated configuration-file of the server contains the given path.
        /// </summary>
        /// <remarks>
        /// The file is read as text and not as a document, because what this asks about is the path itself and not
        /// the shape the serializer gives it: an element which is named or nested differently is no reason for this
        /// testcase to fail, while a path which is not in there at all is.
        /// </remarks>
        /// <param name="toolsFolder">The tools-folder of the started server, which lies next to its configuration-folder.</param>
        /// <param name="path">The path which has to stand in that file.</param>
        private static bool TheConfigurationFileStates(string toolsFolder, string path)
        {
            string configurationFile = Path.Combine(toolsFolder, "..", "Configuration", "Configuration.xml");
            Assert.IsTrue(File.Exists(configurationFile), $"The server did not generate the configuration-file \"{configurationFile}\".");
            return File.ReadAllText(configurationFile).Contains(path, StringComparison.Ordinal);
        }

        /// <summary>
        /// States whether the started server answers. The maintenance-health-check-route is asked, because this
        /// testcase declares no mcp-server which could be asked instead and that route needs no access-token.
        /// </summary>
        private static bool ItIsUp()
        {
            try
            {
                using HttpClient client = new HttpClient();
                using HttpResponseMessage response = client.Send(new HttpRequestMessage(HttpMethod.Get, $"http://127.0.0.1:{HTTP.DefaultPort}/API/Other/Maintenance/HealthCheck"));
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }
    }
}
