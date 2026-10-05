using GenericMCPServerBackend.Core.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace GenericMCPServerBackend.Tests.Testcases.Services
{
    /// <summary>
    /// Covers the rules about the clients an administrator creates, which the business-logic-service decides.
    /// </summary>
    /// <remarks>
    /// Every testcase uses its own folder in a temporary place, so none of them depends on the clients of another one.
    /// The business-logic-service is built with the client-service alone: creating a client touches nothing else, and
    /// a testcase which handed it services it does not use would state a dependency which does not exist.
    /// </remarks>
    [TestClass]
    public class ClientAdministrationTests
    {
        private static string CreateFolder()
        {
            string folder = Path.Combine(Path.GetTempPath(), "GenericMCPServerBackendTests", Guid.NewGuid().ToString());
            Directory.CreateDirectory(folder);
            return folder;
        }

        private static IBusinessLogicService CreateBusinessLogicService(string folder, out IClientService clientService)
        {
            clientService = new ClientService(Path.Combine(folder, ClientService.FileName));
            return new BusinessLogicService(null, Path.Combine(folder, "VirtualEnvironments"), clientService, null, null, null, null);
        }

        /// <remarks>
        /// A client which is created a second time must not replace the first one: that would hand out a new
        /// administration-token for a name somebody else already holds and would take the access of the existing
        /// client away, without anybody having stated that. The failure therefore has to be visible at the moment the
        /// name is taken.
        /// </remarks>
        [TestMethod]
        [TestProperty(nameof(GRYLibrary.Core.Misc.TestKind), nameof(GRYLibrary.Core.Misc.TestKind.UnitTest))]
        public void AClientWhoseNameAlreadyExistsCanNotBeCreated()
        {
            // arrange
            string folder = CreateFolder();
            try
            {
                IBusinessLogicService businessLogicService = CreateBusinessLogicService(folder, out IClientService clientService);
                string tokenOfTheFirstOne = businessLogicService.CreateClient("myclient");

                // act & assert
                Assert.ThrowsException<InvalidOperationException>(() => businessLogicService.CreateClient("myclient"));

                // The existing client is untouched: it is still there exactly once and it still administrates itself
                // with the token it received, which is what the refusal is there to protect.
                Assert.AreEqual(1, clientService.GetClients().Count, "The refused creation must not have added a second row for that name.");
                Assert.IsTrue(clientService.TryGetClientOfAdministrationToken(tokenOfTheFirstOne, out string clientOfTheToken), "The token of the existing client has to keep working.");
                Assert.AreEqual("myclient", clientOfTheToken);
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }

        /// <remarks>
        /// The name of a client is compared without case, so "MyClient" is the same client as "myclient". A creation
        /// which differs only in case would otherwise produce two rows which every later lookup would have to choose
        /// between.
        /// </remarks>
        [TestMethod]
        [TestProperty(nameof(GRYLibrary.Core.Misc.TestKind), nameof(GRYLibrary.Core.Misc.TestKind.UnitTest))]
        public void AClientWhoseNameDiffersOnlyInCaseCanNotBeCreatedEither()
        {
            // arrange
            string folder = CreateFolder();
            try
            {
                IBusinessLogicService businessLogicService = CreateBusinessLogicService(folder, out IClientService clientService);
                businessLogicService.CreateClient("myclient");

                // act & assert
                Assert.ThrowsException<InvalidOperationException>(() => businessLogicService.CreateClient("MyClient"));
                Assert.AreEqual(1, clientService.GetClients().Count);
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }
        /// <remarks>
        /// A new administration-token for a client which does not exist would be a token nobody can use and a row
        /// nobody asked for, so the attempt has to fail instead of doing nothing quietly.
        /// </remarks>
        [TestMethod]
        [TestProperty(nameof(GRYLibrary.Core.Misc.TestKind), nameof(GRYLibrary.Core.Misc.TestKind.UnitTest))]
        public void TheAdministrationTokenOfAClientWhichDoesNotExistCanNotBeRenewed()
        {
            // arrange
            string folder = CreateFolder();
            try
            {
                IBusinessLogicService businessLogicService = CreateBusinessLogicService(folder, out IClientService clientService);

                // act & assert
                Assert.ThrowsException<InvalidOperationException>(() => businessLogicService.RenewClientAdministrationToken("myclient"));
                Assert.AreEqual(0, clientService.GetClients().Count, "The refused renewal must not have created that client.");
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }

        /// <remarks>
        /// This is what the semaphore around the accesses to the csv-files is for. Asking whether the name is free and
        /// writing the client are two steps, so without that serialization several requests which arrive at the same
        /// time could all pass the question and all write - and the file would hold the same client more than once.
        /// </remarks>
        [TestMethod]
        [TestProperty(nameof(GRYLibrary.Core.Misc.TestKind), nameof(GRYLibrary.Core.Misc.TestKind.UnitTest))]
        public void OfSeveralCreationsOfTheSameClientAtTheSameTimeExactlyOneSucceeds()
        {
            // arrange
            const int amountOfAttempts = 16;
            string folder = CreateFolder();
            try
            {
                IBusinessLogicService businessLogicService = CreateBusinessLogicService(folder, out IClientService clientService);

                // act: every attempt uses the same name, so exactly one of them may win
                Task<string>[] attempts = Enumerable.Range(0, amountOfAttempts)
                    .Select(_ => Task.Run(() => businessLogicService.CreateClient("myclient")))
                    .ToArray();
                List<string> tokens = new List<string>();
                List<Exception> failures = new List<Exception>();
                foreach (Task<string> attempt in attempts)
                {
                    try
                    {
                        tokens.Add(attempt.GetAwaiter().GetResult());
                    }
                    catch (Exception exception)
                    {
                        failures.Add(exception);
                    }
                }

                // assert
                Assert.AreEqual(1, tokens.Count, "Exactly one attempt may have received a token.");
                Assert.AreEqual(amountOfAttempts - 1, failures.Count);
                Assert.IsTrue(failures.All(failure => failure is InvalidOperationException), "Every other attempt has to fail because the name is taken.");
                Assert.AreEqual(1, clientService.GetClients().Count, "The client may stand in the file exactly once.");
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }

    }
}
