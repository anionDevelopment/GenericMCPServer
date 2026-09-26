using GenericMCPServerBackend.Core.Misc;
using GenericMCPServerBackend.Tests.TestUtilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Net;
using System.Net.Http;
using System.Text.Json;

namespace GenericMCPServerBackend.Tests.Testcases.Integration
{
    /// <summary>
    /// Exercises the routes with which an administrator manages clients and roles and with which a client manages its
    /// own access-tokens.
    /// </summary>
    /// <remarks>
    /// The testcases go over real http, because the interesting part is exactly the part which a unit-test does not
    /// see: whether the routes are hosted at all and whether a request without the right token is refused.
    /// </remarks>
    [TestClass]
    public class AdministrationTests
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

        private static (HttpStatusCode StatusCode, string Answer) Send(IntegrationTestFramework framework, HttpMethod method, string route, string headerName, string token)
        {
            using HttpClient client = new HttpClient();
            using HttpRequestMessage request = new HttpRequestMessage(method, $"{framework.GetServerURL()}{route}");
            if (token != null)
            {
                request.Headers.Add(headerName, token);
            }
            HttpResponseMessage response = client.Send(request);
            return (response.StatusCode, response.Content.ReadAsStringAsync().GetAwaiter().GetResult());
        }

        /// <remarks>
        /// This is the whole way from an empty server to a working client: an administrator creates a role and a
        /// client, puts the client into the role, hands the client-administration-token over, and whoever runs that
        /// client creates an access-token with it and reaches the mcp-server which that role may use.
        /// </remarks>
        [TestMethod]
        [TestProperty(nameof(GRYLibrary.Core.Misc.TestKind), nameof(GRYLibrary.Core.Misc.TestKind.IntegrationTest))]
        public void AnAdministratorCreatesAClientWhichThenCreatesItsOwnAccessToken()
        {
            // arrange
            using IntegrationTestFramework framework = new IntegrationTestFramework();

            // act: the administrator creates the role, the client and the assignment
            (HttpStatusCode statusCodeOfTheRole, string _) = Send(framework, HttpMethod.Post, "/API/v1/Admin/Roles/second-role", AccessTokenValidatorMiddleware.AdminTokenHeaderName, framework.AdminToken);
            (HttpStatusCode statusCodeOfTheClient, string answerOfTheClient) = Send(framework, HttpMethod.Post, "/API/v1/Admin/Clients/second-client", AccessTokenValidatorMiddleware.AdminTokenHeaderName, framework.AdminToken);
            (HttpStatusCode statusCodeOfTheAssignment, string _) = Send(framework, HttpMethod.Post, "/API/v1/Admin/Clients/second-client/Roles/second-role", AccessTokenValidatorMiddleware.AdminTokenHeaderName, framework.AdminToken);

            // assert: the administration-token is part of the answer and exists only there
            Assert.AreEqual(HttpStatusCode.OK, statusCodeOfTheRole);
            Assert.AreEqual(HttpStatusCode.OK, statusCodeOfTheClient);
            Assert.AreEqual(HttpStatusCode.OK, statusCodeOfTheAssignment);
            string clientAdministrationToken = JsonDocument.Parse(answerOfTheClient).RootElement.GetProperty("clientAdministrationToken").GetString();
            Assert.IsFalse(string.IsNullOrWhiteSpace(clientAdministrationToken));

            // act: the client creates an access-token with its own token
            (HttpStatusCode statusCodeOfTheAccessToken, string answerOfTheAccessToken) = Send(framework, HttpMethod.Post, "/API/v1/ClientAdministration/AccessTokens?validForDays=1", AccessTokenValidatorMiddleware.ClientAdministrationTokenHeaderName, clientAdministrationToken);

            // assert
            Assert.AreEqual(HttpStatusCode.OK, statusCodeOfTheAccessToken);
            JsonElement answer = JsonDocument.Parse(answerOfTheAccessToken).RootElement;
            Assert.AreEqual("second-client", answer.GetProperty("client").GetString());
            string accessToken = answer.GetProperty("accessToken").GetString();
            Assert.IsFalse(string.IsNullOrWhiteSpace(accessToken));

            // assert: that access-token reaches an mcp-server only when a role of its client may use it. The mcp-server
            // of the example-data belongs to another role, so this client does not reach it.
            (HttpStatusCode statusCodeOfTheCall, string _) = framework.SendMCPMessageAndGetStatusCode(@"{""jsonrpc"":""2.0"",""id"":1,""method"":""tools/list""}", ExampleDataCreator.UrlNamespace, accessToken);
            Assert.AreEqual(HttpStatusCode.Unauthorized, statusCodeOfTheCall, "A client which is in no role of that mcp-server does not reach it, even with a valid token.");
        }

        /// <remarks>
        /// Without the right token these routes must not answer at all: they create and remove accesses.
        /// </remarks>
        [TestMethod]
        [TestProperty(nameof(GRYLibrary.Core.Misc.TestKind), nameof(GRYLibrary.Core.Misc.TestKind.IntegrationTest))]
        public void TheAdministrationIsNotReachableWithoutTheRightToken()
        {
            // arrange
            using IntegrationTestFramework framework = new IntegrationTestFramework();

            // act & assert: no token, a wrong token and the token of a client are all not enough for the admin-routes
            Assert.AreEqual(HttpStatusCode.Unauthorized, Send(framework, HttpMethod.Get, "/API/v1/Admin/Clients", AccessTokenValidatorMiddleware.AdminTokenHeaderName, null).StatusCode);
            Assert.AreEqual(HttpStatusCode.Unauthorized, Send(framework, HttpMethod.Get, "/API/v1/Admin/Clients", AccessTokenValidatorMiddleware.AdminTokenHeaderName, "something-else").StatusCode);
            Assert.AreEqual(HttpStatusCode.Unauthorized, Send(framework, HttpMethod.Get, "/API/v1/Admin/Clients", AccessTokenValidatorMiddleware.AdminTokenHeaderName, framework.ClientAdministrationToken).StatusCode, "The token of a client must not open the routes of an administrator.");

            // act & assert: the same holds for the routes of a client
            Assert.AreEqual(HttpStatusCode.Unauthorized, Send(framework, HttpMethod.Get, "/API/v1/ClientAdministration/AccessTokens", AccessTokenValidatorMiddleware.ClientAdministrationTokenHeaderName, null).StatusCode);
            Assert.AreEqual(HttpStatusCode.Unauthorized, Send(framework, HttpMethod.Get, "/API/v1/ClientAdministration/AccessTokens", AccessTokenValidatorMiddleware.ClientAdministrationTokenHeaderName, framework.AdminToken).StatusCode, "The token of an administrator is no token of a client.");
        }

        /// <remarks>
        /// An administrator exchanges the token of a client when it leaked or when the person who held it left. What
        /// matters is that the previous one stops working at once - a renewal which leaves the old token valid would
        /// be no renewal.
        /// </remarks>
        [TestMethod]
        [TestProperty(nameof(GRYLibrary.Core.Misc.TestKind), nameof(GRYLibrary.Core.Misc.TestKind.IntegrationTest))]
        public void AnAdministratorRenewsTheTokenOfAClient()
        {
            // arrange
            using IntegrationTestFramework framework = new IntegrationTestFramework();
            string tokenSoFar = framework.ClientAdministrationToken;

            // act
            (HttpStatusCode statusCode, string answer) = Send(framework, HttpMethod.Post, $"/API/v1/Admin/Clients/{IntegrationTestFramework.ClientName}/AdministrationToken", AccessTokenValidatorMiddleware.AdminTokenHeaderName, framework.AdminToken);

            // assert
            Assert.AreEqual(HttpStatusCode.OK, statusCode);
            string newToken = JsonDocument.Parse(answer).RootElement.GetProperty("clientAdministrationToken").GetString();
            Assert.AreNotEqual(tokenSoFar, newToken);
            Assert.AreEqual(HttpStatusCode.OK, Send(framework, HttpMethod.Get, "/API/v1/ClientAdministration/AccessTokens", AccessTokenValidatorMiddleware.ClientAdministrationTokenHeaderName, newToken).StatusCode, "The new token has to work.");
            Assert.AreEqual(HttpStatusCode.Unauthorized, Send(framework, HttpMethod.Get, "/API/v1/ClientAdministration/AccessTokens", AccessTokenValidatorMiddleware.ClientAdministrationTokenHeaderName, tokenSoFar).StatusCode, "The previous token must not work anymore.");
            // The access-tokens of the client are untouched by this: they are what the client uses, and who may
            // administrate the client is another question.
            Assert.AreEqual(HttpStatusCode.OK, framework.SendMCPMessageAndGetStatusCode(@"{""jsonrpc"":""2.0"",""id"":1,""method"":""tools/list""}", ExampleDataCreator.UrlNamespace, framework.AccessToken).StatusCode);
        }

        /// <remarks>
        /// A renewal for a client which does not exist is stated as such instead of creating one.
        /// </remarks>
        [TestMethod]
        [TestProperty(nameof(GRYLibrary.Core.Misc.TestKind), nameof(GRYLibrary.Core.Misc.TestKind.IntegrationTest))]
        public void ATokenIsNotRenewedForAClientWhichDoesNotExist()
        {
            // arrange
            using IntegrationTestFramework framework = new IntegrationTestFramework();

            // act & assert
            Assert.AreEqual(HttpStatusCode.NotFound, Send(framework, HttpMethod.Post, "/API/v1/Admin/Clients/nobody/AdministrationToken", AccessTokenValidatorMiddleware.AdminTokenHeaderName, framework.AdminToken).StatusCode);
        }

        /// <remarks>
        /// A client sees and changes its own access-tokens and nothing else - it can not even name another client.
        /// </remarks>
        [TestMethod]
        [TestProperty(nameof(GRYLibrary.Core.Misc.TestKind), nameof(GRYLibrary.Core.Misc.TestKind.IntegrationTest))]
        public void AClientAdministratesItsOwnAccessTokens()
        {
            // arrange
            using IntegrationTestFramework framework = new IntegrationTestFramework();

            // act
            (HttpStatusCode statusCodeOfTheList, string list) = Send(framework, HttpMethod.Get, "/API/v1/ClientAdministration/AccessTokens", AccessTokenValidatorMiddleware.ClientAdministrationTokenHeaderName, framework.ClientAdministrationToken);

            // assert: exactly the one token which the framework created for this client
            Assert.AreEqual(HttpStatusCode.OK, statusCodeOfTheList);
            JsonElement tokens = JsonDocument.Parse(list).RootElement;
            Assert.AreEqual(1, tokens.GetArrayLength());
            string hash = tokens[0].GetProperty("hash").GetString();
            Assert.IsFalse(string.IsNullOrWhiteSpace(hash));
            Assert.IsFalse(list.Contains(framework.AccessToken, StringComparison.Ordinal), "The list must contain the hash of a token and never the token itself.");

            // act: the client removes that token
            (HttpStatusCode statusCodeOfTheRemoval, string _) = Send(framework, HttpMethod.Delete, $"/API/v1/ClientAdministration/AccessTokens/{hash}", AccessTokenValidatorMiddleware.ClientAdministrationTokenHeaderName, framework.ClientAdministrationToken);

            // assert: the token does not reach the mcp-server anymore, immediately and without a restart
            Assert.AreEqual(HttpStatusCode.NoContent, statusCodeOfTheRemoval);
            (HttpStatusCode statusCodeOfTheCall, string _) = framework.SendMCPMessageAndGetStatusCode(@"{""jsonrpc"":""2.0"",""id"":1,""method"":""tools/list""}", ExampleDataCreator.UrlNamespace, framework.AccessToken);
            Assert.AreEqual(HttpStatusCode.Unauthorized, statusCodeOfTheCall);
        }

        /// <remarks>
        /// Taking a role away from a client is how an access is withdrawn without touching the client itself. The
        /// access-token of that client stays valid - what it does not reach anymore is the mcp-server which that role
        /// may use, and that takes effect at the next request, because the roles are read per request.
        /// </remarks>
        [TestMethod]
        [TestProperty(nameof(GRYLibrary.Core.Misc.TestKind), nameof(GRYLibrary.Core.Misc.TestKind.IntegrationTest))]
        public void RemovingARoleOfAClientTakesAwayItsAccess()
        {
            // arrange
            using IntegrationTestFramework framework = new IntegrationTestFramework();

            // act
            (HttpStatusCode statusCode, string _) = Send(framework, HttpMethod.Delete, $"/API/v1/Admin/Clients/{IntegrationTestFramework.ClientName}/Roles/{ExampleDataCreator.RoleName}", AccessTokenValidatorMiddleware.AdminTokenHeaderName, framework.AdminToken);

            // assert
            Assert.AreEqual(HttpStatusCode.NoContent, statusCode);
            Assert.AreEqual(HttpStatusCode.Unauthorized, framework.SendMCPMessageAndGetStatusCode(@"{""jsonrpc"":""2.0"",""id"":1,""method"":""tools/list""}", ExampleDataCreator.UrlNamespace, framework.AccessToken).StatusCode, "A client which is in no role of that mcp-server does not reach it anymore, immediately and without a restart.");
            (HttpStatusCode statusCodeOfTheRoles, string roles) = Send(framework, HttpMethod.Get, $"/API/v1/Admin/Clients/{IntegrationTestFramework.ClientName}/Roles", AccessTokenValidatorMiddleware.AdminTokenHeaderName, framework.AdminToken);
            Assert.AreEqual(HttpStatusCode.OK, statusCodeOfTheRoles);
            Assert.AreEqual(0, JsonDocument.Parse(roles).RootElement.GetArrayLength(), "The client is in no role anymore.");
        }

        /// <remarks>
        /// A client which was removed must not leave anything behind: its access-tokens and its roles are gone with it.
        /// Otherwise a client which is created again under the same name later would silently inherit the accesses of
        /// its predecessor.
        /// </remarks>
        [TestMethod]
        [TestProperty(nameof(GRYLibrary.Core.Misc.TestKind), nameof(GRYLibrary.Core.Misc.TestKind.IntegrationTest))]
        public void DeletingAClientTakesAwayItsTokensAndItsRoles()
        {
            // arrange
            using IntegrationTestFramework framework = new IntegrationTestFramework();

            // act
            (HttpStatusCode statusCode, string _) = Send(framework, HttpMethod.Delete, $"/API/v1/Admin/Clients/{IntegrationTestFramework.ClientName}", AccessTokenValidatorMiddleware.AdminTokenHeaderName, framework.AdminToken);

            // assert
            Assert.AreEqual(HttpStatusCode.NoContent, statusCode);
            Assert.AreEqual(HttpStatusCode.Unauthorized, framework.SendMCPMessageAndGetStatusCode(@"{""jsonrpc"":""2.0"",""id"":1,""method"":""tools/list""}", ExampleDataCreator.UrlNamespace, framework.AccessToken).StatusCode, "The access-token of a client which does not exist anymore reaches nothing.");
            Assert.AreEqual(HttpStatusCode.Unauthorized, Send(framework, HttpMethod.Get, "/API/v1/ClientAdministration/AccessTokens", AccessTokenValidatorMiddleware.ClientAdministrationTokenHeaderName, framework.ClientAdministrationToken).StatusCode, "The client-administration-token of a client which does not exist anymore administrates nothing.");
            (HttpStatusCode statusCodeOfTheClients, string clients) = Send(framework, HttpMethod.Get, "/API/v1/Admin/Clients", AccessTokenValidatorMiddleware.AdminTokenHeaderName, framework.AdminToken);
            Assert.AreEqual(HttpStatusCode.OK, statusCodeOfTheClients);
            Assert.IsFalse(clients.Contains(IntegrationTestFramework.ClientName, StringComparison.Ordinal), "The client is not one of the clients anymore.");
        }

        /// <remarks>
        /// A role which is removed takes every assignment of it with it, so the access which it granted ends as well. A
        /// role whose assignments stayed would bring its former members back as soon as somebody creates a role of that
        /// name again.
        /// </remarks>
        [TestMethod]
        [TestProperty(nameof(GRYLibrary.Core.Misc.TestKind), nameof(GRYLibrary.Core.Misc.TestKind.IntegrationTest))]
        public void DeletingARoleTakesAwayTheAccessWhichItGranted()
        {
            // arrange
            using IntegrationTestFramework framework = new IntegrationTestFramework();

            // act
            (HttpStatusCode statusCode, string _) = Send(framework, HttpMethod.Delete, $"/API/v1/Admin/Roles/{ExampleDataCreator.RoleName}", AccessTokenValidatorMiddleware.AdminTokenHeaderName, framework.AdminToken);

            // assert
            Assert.AreEqual(HttpStatusCode.NoContent, statusCode);
            Assert.AreEqual(HttpStatusCode.Unauthorized, framework.SendMCPMessageAndGetStatusCode(@"{""jsonrpc"":""2.0"",""id"":1,""method"":""tools/list""}", ExampleDataCreator.UrlNamespace, framework.AccessToken).StatusCode, "The mcp-server of that role is not reachable anymore, although the access-token itself is untouched.");
            (HttpStatusCode statusCodeOfTheRoles, string existingRoles) = Send(framework, HttpMethod.Get, "/API/v1/Admin/Roles", AccessTokenValidatorMiddleware.AdminTokenHeaderName, framework.AdminToken);
            Assert.AreEqual(HttpStatusCode.OK, statusCodeOfTheRoles);
            Assert.IsFalse(existingRoles.Contains(ExampleDataCreator.RoleName, StringComparison.Ordinal), "The role does not exist anymore.");
            (HttpStatusCode statusCodeOfTheRolesOfTheClient, string rolesOfTheClient) = Send(framework, HttpMethod.Get, $"/API/v1/Admin/Clients/{IntegrationTestFramework.ClientName}/Roles", AccessTokenValidatorMiddleware.AdminTokenHeaderName, framework.AdminToken);
            Assert.AreEqual(HttpStatusCode.OK, statusCodeOfTheRolesOfTheClient);
            Assert.AreEqual(0, JsonDocument.Parse(rolesOfTheClient).RootElement.GetArrayLength(), "The assignment of that role is gone with it.");
        }

        /// <remarks>
        /// This is what an administrator uses to see who exists: the clients with their roles and with how many
        /// access-tokens each of them holds. It must state no token at all, because only the hashes are stored and a
        /// list which contained a token would hand out an access which is supposed to be readable exactly once.
        /// </remarks>
        [TestMethod]
        [TestProperty(nameof(GRYLibrary.Core.Misc.TestKind), nameof(GRYLibrary.Core.Misc.TestKind.IntegrationTest))]
        public void TheClientsWhichExistAreListedWithTheirRolesAndWithoutAnyToken()
        {
            // arrange
            using IntegrationTestFramework framework = new IntegrationTestFramework();

            // act
            (HttpStatusCode statusCode, string answer) = Send(framework, HttpMethod.Get, "/API/v1/Admin/Clients", AccessTokenValidatorMiddleware.AdminTokenHeaderName, framework.AdminToken);

            // assert
            Assert.AreEqual(HttpStatusCode.OK, statusCode);
            JsonElement clients = JsonDocument.Parse(answer).RootElement;
            Assert.AreEqual(1, clients.GetArrayLength());
            Assert.AreEqual(IntegrationTestFramework.ClientName, clients[0].GetProperty("client").GetString());
            Assert.AreEqual(1, clients[0].GetProperty("roles").GetArrayLength());
            Assert.AreEqual(ExampleDataCreator.RoleName, clients[0].GetProperty("roles")[0].GetString());
            Assert.AreEqual(1, clients[0].GetProperty("amountOfAccessTokens").GetInt32());
            Assert.IsFalse(answer.Contains(framework.AccessToken, StringComparison.Ordinal), "The list must never contain an access-token.");
            Assert.IsFalse(answer.Contains(framework.ClientAdministrationToken, StringComparison.Ordinal), "The list must never contain a client-administration-token.");
        }

        /// <remarks>
        /// A removal which found nothing is stated as such and is not answered as if it had removed something: an
        /// administrator who mistyped a name would otherwise believe that an access is gone while it still exists.
        /// </remarks>
        [TestMethod]
        [TestProperty(nameof(GRYLibrary.Core.Misc.TestKind), nameof(GRYLibrary.Core.Misc.TestKind.IntegrationTest))]
        public void TheAdministrationStatesWhatDoesNotExistInsteadOfAnsweringAsIfItHadRemovedIt()
        {
            // arrange
            using IntegrationTestFramework framework = new IntegrationTestFramework();

            // act & assert
            Assert.AreEqual(HttpStatusCode.NotFound, Send(framework, HttpMethod.Delete, "/API/v1/Admin/Clients/nobody", AccessTokenValidatorMiddleware.AdminTokenHeaderName, framework.AdminToken).StatusCode, "A client of that name does not exist.");
            Assert.AreEqual(HttpStatusCode.NotFound, Send(framework, HttpMethod.Delete, "/API/v1/Admin/Roles/no-role", AccessTokenValidatorMiddleware.AdminTokenHeaderName, framework.AdminToken).StatusCode, "A role of that name does not exist.");
            Assert.AreEqual(HttpStatusCode.NotFound, Send(framework, HttpMethod.Delete, $"/API/v1/Admin/Clients/{IntegrationTestFramework.ClientName}/Roles/no-role", AccessTokenValidatorMiddleware.AdminTokenHeaderName, framework.AdminToken).StatusCode, "The client exists, but it is not in a role of that name.");
            Assert.AreEqual(HttpStatusCode.NotFound, Send(framework, HttpMethod.Get, "/API/v1/Admin/Clients/nobody/Roles", AccessTokenValidatorMiddleware.AdminTokenHeaderName, framework.AdminToken).StatusCode, "The roles of a client which does not exist are no empty list.");
        }
    }
}
