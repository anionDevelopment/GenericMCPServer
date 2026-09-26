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
        /// The path of the declaration is read from the configuration-file of the server and from nowhere else. What
        /// can be stated from outside is which value that file is generated with, which is what this exercises: the
        /// server has to resolve the stated name against its tools-folder instead of the default "Tools.json".
        /// </remarks>
        [TestMethod]
        [TestProperty(nameof(GRYLibrary.Core.Misc.TestKind), nameof(GRYLibrary.Core.Misc.TestKind.IntegrationTest))]
        public void TheInitialPathOfTheDeclarationCanBeStatedWhileStarting()
        {
            // arrange
            string toolsFolder = null;
            Program program = new Program()
            {
                RunAsync = false,
                ListenOnEveryIP = true,
                // The tools-folder is not wiped between two test-runs, so what an earlier one left there is removed:
                // a "Tools.json" of an earlier run would make the assertion below fail for a reason which is not the
                // one it asks about. The folder itself is recreated right away, because the server expects the folder
                // of this run to exist and only creates the declaration in it.
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
                $"--{nameof(GenericMCPServerBackend.Core.Configuration.CommandlineParameter.InitialToolsDefinitionFilePath)}", "./Declaration.json",
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
                Assert.IsTrue(File.Exists(Path.Combine(toolsFolder, "Declaration.json")), "The server has to resolve the stated name against its tools-folder.");
                Assert.IsFalse(File.Exists(Path.Combine(toolsFolder, "Tools.json")), "The default has to be replaced by the stated value and not to be used next to it.");
            }
            finally
            {
                if (program.IsRunning)
                {
                    program.Stop();
                }
            }
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
