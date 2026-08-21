using GenericMCPServerBackend.Core.Misc;
using GenericMCPServerBackend.Core.Services;
using GRYLibrary.Core.Logging.GRYLogger;
using GRYLibrary.Core.Logging.GRYLogger.ConcreteLogTargets;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Linq;
using Console = GRYLibrary.Core.Logging.GRYLogger.ConcreteLogTargets.Console;

namespace GenericMCPServerBackend.Tests.Testcases.Services
{
    /// <summary>
    /// Covers the token of the administrator: how it comes into existence, where it is shown and where it is not.
    /// </summary>
    [TestClass]
    public class AdminTokenServiceTests
    {
        private static string CreateFolder()
        {
            string folder = Path.Combine(Path.GetTempPath(), "GenericMCPServerBackendTests", Guid.NewGuid().ToString());
            Directory.CreateDirectory(folder);
            return folder;
        }

        /// <summary>
        /// Creates a log which writes to the console and to a file, the way the log of the server does.
        /// </summary>
        private static GRYLog CreateLog(string logFile)
        {
            // This overload is the one which also sets the base-path of the log; the file-target resolves its file
            // against it, so a log which was created without it writes nowhere.
            GRYLog log = GRYLog.Create(logFile, null, Path.GetDirectoryName(logFile));
            log.Configuration.LogTargets.OfType<Console>().Single().Enabled = true;
            log.Configuration.LogTargets.OfType<LogFile>().Single().Enabled = true;
            return log;
        }

        /// <summary>
        /// Returns the file the given log actually writes to. It is asked instead of being assumed, because the target
        /// resolves its file against the base-path of the log.
        /// </summary>
        private static string GetLogFileOf(GRYLog log)
        {
            return log.Configuration.LogTargets.OfType<LogFile>().Single().File.GetPath(log.BasePath);
        }

        /// <summary>
        /// Runs the given action while the output of the console is collected and returns that output.
        /// </summary>
        private static string GetConsoleOutputOf(Action action)
        {
            TextWriter outputSoFar = System.Console.Out;
            using StringWriter output = new StringWriter();
            System.Console.SetOut(output);
            try
            {
                action();
            }
            finally
            {
                System.Console.SetOut(outputSoFar);
            }
            return output.ToString();
        }

        /// <summary>Returns the token which stands in the given output of the console.</summary>
        private static string GetTokenOf(string consoleOutput)
        {
            string token = consoleOutput.Split(new char[] { ' ', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).SingleOrDefault(word => word.StartsWith(Token.PrefixOfAnAdminToken, StringComparison.Ordinal));
            Assert.IsNotNull(token, $"The token was not written to the console. Its output was: {consoleOutput}");
            return token;
        }

        /// <remarks>
        /// The token is readable exactly once, in the moment it is created, and nowhere else: an administrator reads it
        /// on the console of the starting server, and whoever gets the file of the server into their hands holds no
        /// access.
        /// </remarks>
        [TestMethod]
        [TestProperty(nameof(GRYLibrary.Core.Misc.TestKind), nameof(GRYLibrary.Core.Misc.TestKind.UnitTest))]
        public void TheTokenIsShownOnTheConsoleAndOnlyItsHashIsStored()
        {
            // arrange
            string folder = CreateFolder();
            try
            {
                string file = Path.Combine(folder, AdminTokenService.FileName);
                string logFile = Path.Combine(folder, "Log.log");
                using GRYLog log = CreateLog(logFile);

                // act
                IAdminTokenService adminTokenService = null;
                string consoleOutput = GetConsoleOutputOf(() => adminTokenService = new AdminTokenService(file, log));

                // assert
                string token = GetTokenOf(consoleOutput);
                Assert.IsTrue(adminTokenService.Matches(token));
                Assert.IsFalse(adminTokenService.Matches("gmcpadmin_00000000000000000000000000000000"));
                Assert.IsFalse(adminTokenService.Matches(string.Empty));
                string contentOfTheFile = File.ReadAllText(file);
                Assert.IsFalse(contentOfTheFile.Contains(token, StringComparison.Ordinal), "The file must contain the hash of the token and not the token.");
                Assert.IsTrue(contentOfTheFile.Contains(Token.GetHash(token), StringComparison.OrdinalIgnoreCase));
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }

        /// <remarks>
        /// A log-file stays readable for as long as it exists, and it is copied, shipped and archived. The token is
        /// therefore written while the file-targets are switched off - and they work again afterwards, which is the
        /// second half of the promise.
        /// </remarks>
        [TestMethod]
        [TestProperty(nameof(GRYLibrary.Core.Misc.TestKind), nameof(GRYLibrary.Core.Misc.TestKind.UnitTest))]
        public void TheTokenIsNotWrittenIntoTheLogFile()
        {
            // arrange
            string folder = CreateFolder();
            try
            {
                string file = Path.Combine(folder, AdminTokenService.FileName);
                string logFile = Path.Combine(folder, "Log.log");
                using GRYLog log = CreateLog(logFile);

                // act
                string consoleOutput = GetConsoleOutputOf(() => new AdminTokenService(file, log));
                string token = GetTokenOf(consoleOutput);
                log.Log("This entry belongs into the log-file again.");

                // assert
                string fileTheLogWritesTo = GetLogFileOf(log);
                string contentOfTheLogFile = File.Exists(fileTheLogWritesTo) ? File.ReadAllText(fileTheLogWritesTo) : string.Empty;
                Assert.IsFalse(contentOfTheLogFile.Contains(token, StringComparison.Ordinal), $"The token must not stand in the log-file, but its content is: {contentOfTheLogFile}");
                Assert.IsTrue(contentOfTheLogFile.Contains("This entry belongs into the log-file again.", StringComparison.Ordinal), "The file-target has to be switched on again after the token was written.");
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }

        /// <remarks>
        /// A token which nobody reads is no access but a server which can not be administrated anymore, so this is
        /// stated when it happens and not when somebody tries to use the administration.
        /// </remarks>
        [TestMethod]
        [TestProperty(nameof(GRYLibrary.Core.Misc.TestKind), nameof(GRYLibrary.Core.Misc.TestKind.UnitTest))]
        public void ATokenWhichNobodyWouldSeeIsNotCreated()
        {
            // arrange
            string folder = CreateFolder();
            try
            {
                string file = Path.Combine(folder, AdminTokenService.FileName);
                using GRYLog log = CreateLog(Path.Combine(folder, "Log.log"));
                log.Configuration.LogTargets.OfType<Console>().Single().Enabled = false;

                // act & assert
                Assert.ThrowsException<InvalidOperationException>(() => new AdminTokenService(file, log));
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }

        /// <remarks>
        /// Only the first start creates a token: a server which replaced it at every start would take the access of its
        /// administrator away whenever it restarts.
        /// </remarks>
        [TestMethod]
        [TestProperty(nameof(GRYLibrary.Core.Misc.TestKind), nameof(GRYLibrary.Core.Misc.TestKind.UnitTest))]
        public void AnExistingTokenStaysTheTokenOfTheAdministrator()
        {
            // arrange
            string folder = CreateFolder();
            try
            {
                string file = Path.Combine(folder, AdminTokenService.FileName);
                using GRYLog log = CreateLog(Path.Combine(folder, "Log.log"));
                string token = GetTokenOf(GetConsoleOutputOf(() => new AdminTokenService(file, log)));

                // act
                IAdminTokenService adminTokenServiceOfTheSecondStart = null;
                string consoleOutputOfTheSecondStart = GetConsoleOutputOf(() => adminTokenServiceOfTheSecondStart = new AdminTokenService(file, log));

                // assert
                Assert.IsTrue(adminTokenServiceOfTheSecondStart.Matches(token));
                Assert.IsFalse(consoleOutputOfTheSecondStart.Contains(Token.PrefixOfAnAdminToken, StringComparison.Ordinal), "A start which creates no token must not show one.");
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }
    }
}
