using GenericMCPServerBackend.Core.Model;
using GenericMCPServerBackend.Core.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;

namespace GenericMCPServerBackend.Tests.Testcases.Services
{
    /// <summary>
    /// Covers the access-tokens with which a client reaches the server.
    /// </summary>
    /// <remarks>
    /// Every testcase uses its own file in a temporary folder, so none of them depends on the tokens of another one.
    /// </remarks>
    [TestClass]
    public class AccessTokenServiceTests
    {
        private static string CreateFile()
        {
            return Path.Combine(Path.GetTempPath(), "GenericMCPServerBackendTests", Guid.NewGuid().ToString(), "AccessTokens.csv");
        }

        private static void DeleteFolderOf(string file)
        {
            string? folder = Path.GetDirectoryName(file);
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, true);
            }
        }

        /// <remarks>
        /// An administrator has to find the place where the tokens belong without having to know the format of the
        /// file, so it is created with its header.
        /// </remarks>
        [TestMethod]
        [TestProperty(nameof(GRYLibrary.Core.Misc.TestKind), nameof(GRYLibrary.Core.Misc.TestKind.UnitTest))]
        public void TheFileIsCreatedWhenItDoesNotExistYet()
        {
            // arrange
            string file = CreateFile();
            try
            {
                // act
                IAccessTokenService accessTokenService = new AccessTokenService(file);

                // assert
                Assert.IsTrue(File.Exists(file));
                Assert.AreEqual("client;tokenhash;validuntil", File.ReadAllLines(file)[0]);
                Assert.AreEqual(0, new AccessTokenService(file).ReadAll().Count, "A file which was just created contains no token.");
            }
            finally
            {
                DeleteFolderOf(file);
            }
        }

        [TestMethod]
        [TestProperty(nameof(GRYLibrary.Core.Misc.TestKind), nameof(GRYLibrary.Core.Misc.TestKind.UnitTest))]
        public void ACreatedTokenBelongsToItsClient()
        {
            // arrange
            string file = CreateFile();
            try
            {
                IAccessTokenService accessTokenService = new AccessTokenService(file);

                // act
                string token = accessTokenService.CreateAccessToken("team-a", 30);

                // assert
                Assert.IsTrue(accessTokenService.TryGetClient(token, out string client));
                Assert.AreEqual("team-a", client);
            }
            finally
            {
                DeleteFolderOf(file);
            }
        }

        /// <remarks>
        /// Only the hash is stored, so whoever gets the file into their hands holds no access.
        /// </remarks>
        [TestMethod]
        [TestProperty(nameof(GRYLibrary.Core.Misc.TestKind), nameof(GRYLibrary.Core.Misc.TestKind.UnitTest))]
        public void TheTokenItselfIsNotStored()
        {
            // arrange
            string file = CreateFile();
            try
            {
                IAccessTokenService accessTokenService = new AccessTokenService(file);

                // act
                string token = accessTokenService.CreateAccessToken("team-a", 30);

                // assert
                Assert.IsFalse(File.ReadAllText(file).Contains(token, StringComparison.Ordinal), "The file must contain the hash of the token and not the token.");
            }
            finally
            {
                DeleteFolderOf(file);
            }
        }

        /// <remarks>
        /// This is what makes an exchange possible without an interruption: the new token is created, the client is
        /// switched over, and only then the old one is removed.
        /// </remarks>
        [TestMethod]
        [TestProperty(nameof(GRYLibrary.Core.Misc.TestKind), nameof(GRYLibrary.Core.Misc.TestKind.UnitTest))]
        public void AClientCanHaveMoreThanOneValidTokenAtTheSameTime()
        {
            // arrange
            string file = CreateFile();
            try
            {
                IAccessTokenService accessTokenService = new AccessTokenService(file);

                // act
                string firstToken = accessTokenService.CreateAccessToken("team-a", 30);
                string secondToken = accessTokenService.CreateAccessToken("team-a", 60);

                // assert
                Assert.IsTrue(accessTokenService.TryGetClient(firstToken, out string clientOfTheFirstToken));
                Assert.IsTrue(accessTokenService.TryGetClient(secondToken, out string clientOfTheSecondToken));
                Assert.AreEqual("team-a", clientOfTheFirstToken);
                Assert.AreEqual("team-a", clientOfTheSecondToken);
                Assert.AreNotEqual(firstToken, secondToken);
            }
            finally
            {
                DeleteFolderOf(file);
            }
        }

        [TestMethod]
        [TestProperty(nameof(GRYLibrary.Core.Misc.TestKind), nameof(GRYLibrary.Core.Misc.TestKind.UnitTest))]
        public void ATokenWhichWasNotCreatedBelongsToNobody()
        {
            // arrange
            string file = CreateFile();
            try
            {
                IAccessTokenService accessTokenService = new AccessTokenService(file);
                accessTokenService.CreateAccessToken("team-a", 30);

                // act & assert
                Assert.IsFalse(accessTokenService.TryGetClient("gmcp_0000000000000000000000000000000000000000000000000000000000000000", out string _));
                Assert.IsFalse(accessTokenService.TryGetClient(string.Empty, out string _));
                Assert.IsFalse(accessTokenService.TryGetClient(null, out string _));
            }
            finally
            {
                DeleteFolderOf(file);
            }
        }

        /// <remarks>
        /// Every token has an end. A token whose end has passed is not an access anymore, which is checked at every
        /// request and not only when it is created.
        /// </remarks>
        [TestMethod]
        [TestProperty(nameof(GRYLibrary.Core.Misc.TestKind), nameof(GRYLibrary.Core.Misc.TestKind.UnitTest))]
        public void AnExpiredTokenIsNoAccessAnymore()
        {
            // arrange
            string file = CreateFile();
            try
            {
                AccessTokenService accessTokenService = new AccessTokenService(file);
                string token = accessTokenService.CreateAccessToken("team-a", 30);
                // the end of the token is moved into the past, which is what happens by itself when time passes
                IReadOnlyList<AccessToken> tokens = accessTokenService.ReadAll();
                File.WriteAllLines(file, new string[] { "client;tokenhash;validuntil", $"{tokens[0].Client};{tokens[0].Hash};2020-01-01T00:00:00Z" });

                // act & assert
                Assert.IsFalse(accessTokenService.TryGetClient(token, out string _));
            }
            finally
            {
                DeleteFolderOf(file);
            }
        }

        /// <remarks>
        /// A name which contains the separator of the file would turn one token into another one, so it is rejected
        /// where it is stated and not where it would be read.
        /// </remarks>
        [TestMethod]
        [TestProperty(nameof(GRYLibrary.Core.Misc.TestKind), nameof(GRYLibrary.Core.Misc.TestKind.UnitTest))]
        public void AClientNameWhichIsNotUsableIsRejected()
        {
            // arrange
            string file = CreateFile();
            try
            {
                IAccessTokenService accessTokenService = new AccessTokenService(file);

                // act & assert
                Assert.ThrowsException<ArgumentException>(() => accessTokenService.CreateAccessToken("team;a", 30));
                Assert.ThrowsException<ArgumentException>(() => accessTokenService.CreateAccessToken(string.Empty, 30));
                Assert.ThrowsException<ArgumentException>(() => accessTokenService.CreateAccessToken("team-a", 0), "A token which is valid for no day is no token.");
            }
            finally
            {
                DeleteFolderOf(file);
            }
        }

        /// <remarks>
        /// A line which can not be read is not skipped: skipping it would silently take an access away or leave one in
        /// place, and nobody would see which of the two happened.
        /// </remarks>
        [TestMethod]
        [TestProperty(nameof(GRYLibrary.Core.Misc.TestKind), nameof(GRYLibrary.Core.Misc.TestKind.UnitTest))]
        public void ALineWhichCanNotBeReadIsAnError()
        {
            // arrange
            string file = CreateFile();
            try
            {
                AccessTokenService accessTokenService = new AccessTokenService(file);
                File.WriteAllLines(file, new string[] { "client;tokenhash;validuntil", "team-a;abc" });

                // act & assert
                Assert.ThrowsException<InvalidOperationException>(() => accessTokenService.ReadAll());
            }
            finally
            {
                DeleteFolderOf(file);
            }
        }
    }
}
