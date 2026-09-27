using GRYLibrary.Core.APIServer.Settings.Configuration;
using GenericMCPServerBackend.Core;
using GenericMCPServerBackend.Core.Misc;
using GenericMCPServerBackend.Core.Services;
using System.Collections.Generic;
using System.Text.Json;
using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;

namespace GenericMCPServerBackend.Tests.TestUtilities
{
    /// <summary>
    /// Starts the server of this codeunit in the own process and makes it reachable over http, so that a testcase can
    /// exercise the MCP-endpoint the way a real client does instead of calling the c#-objects behind it.
    /// </summary>
    /// <remarks>
    /// Every instance binds the same fixed port, so two of them must never exist at the same time; that is what
    /// <see cref="IntegrationTestServerLock"/> is for.
    /// </remarks>
    public sealed class IntegrationTestFramework : IDisposable
    {
        private readonly Program _Program;
        // Not readonly: it is only created once the started server tells this instance where its tools-folder is (see the constructor below), which
        // happens on the server's own thread while this constructor is still waiting for the server to become ready.
        private ExampleDataCreator _ExampleDataCreator;

        /// <summary>The tools the started server offers come from this example-data.</summary>
        public ExampleDataCreator ExampleData => this._ExampleDataCreator;

        /// <summary>The name of the client the testcases reach the server as.</summary>
        public const string ClientName = "integration-test";

        /// <summary>
        /// The access-token of that client. It is created over the api after the server started, exactly the way it is
        /// created in reality: the folders of the server are not stated from outside, so a testcase can not write into
        /// them - and it should not have to.
        /// </summary>
        public string AccessToken { get; }

        /// <summary>The token with which that client administrates its own access-tokens.</summary>
        public string ClientAdministrationToken { get; }

        /// <summary>
        /// The token with which an administrator reaches the admin-routes of the started server.
        /// </summary>
        /// <remarks>
        /// The server writes the token it created to its log and stores nothing but its hash, so a testcase can not
        /// read it anywhere. It therefore states its own one: the hash in the file of the server is replaced by the
        /// hash of this token, which the server reads at the next request because it reads that file every time.
        /// </remarks>
        public string AdminToken { get; }

        public IntegrationTestFramework()
        {
            // The port is fixed, so a port which is occupied here is held by something outside the test-run (a running
            // server or a second test-run). Failing with that diagnosis is more useful than the "could not start
            // service" which would follow otherwise.
            if (GRYLibrary.Core.Misc.Utilities.PortIsAvailable("127.0.0.1", HTTP.DefaultPort))
            {
                throw new InvalidOperationException($"The port {HTTP.DefaultPort} which the test-server uses is already in use by a process outside the test-run.");
            }
            this._Program = new Program()
            {
                // The server runs in its own thread and blocks there, so it stays up until Stop is called. Running it
                // asynchronously instead would let MainImplementation return immediately, which ends the run.
                RunAsync = false,
                // The server has to listen on every ip: with the domain-only binding the testcases would have to
                // resolve the configured domain, while they address the server under 127.0.0.1.
                ListenOnEveryIP = true,
                // ToolDefinitionsFile is generated as the path of "Tools.json" in the tools-folder, so the example-data is written directly into
                // that folder as soon as the server (on its own thread) reports where it is - before it gets to read that default -
                // instead of pointing the server at some independent location, which is no longer configurable from outside at all.
                // Unlike the configuration-folder, the tools-folder is not wiped between two test-runs, so whatever an earlier run left there is
                // removed here: a test-run which found the declaration of an older one would test that one instead of its own.
                OnToolsFolderResolved = toolsFolder =>
                {
                    GRYLibrary.Core.Misc.Utilities.EnsureDirectoryDoesNotExist(toolsFolder);
                    this._ExampleDataCreator = new ExampleDataCreator(toolsFolder);
                },
            };
            Thread thread = new Thread(() => this._Program.MainImplementation(new string[]
            {
                // No "--RealRun true": without it the server runs in its test-run-mode, which binds plain http on the
                // default-port and therefore needs no certificate.
                // The initialization-state-route is what the readiness-probe below asks, so it has to be available.
                $"--{nameof(GenericMCPServerBackend.Core.Configuration.CommandlineParameter.InitialEnableEndpointInitializationStateValue)}", "true",
            }));
            thread.Start();
            if (!GRYLibrary.Core.Misc.Utilities.RunWithTimeout(() =>
            {
                while (!this.IsReady())
                {
                    Thread.Sleep(TimeSpan.FromMilliseconds(200));
                }
            }, TimeSpan.FromSeconds(120)))
            {
                // The server is stopped before the failure is reported: it holds the fixed port, so leaving it running
                // would make every following testcase fail with "port already in use" and hide the actual reason.
                this.Dispose();
                throw new InvalidOperationException("The test-server did not become ready.");
            }
            this.AdminToken = SetTheTokenOfTheAdministrator(this._Program._AdminTokenService);
            // The role, the client and the access-token are created the way an administrator and the operator of a
            // client create them: over the api of the running server.
            this.SendToTheAdministration(HttpMethod.Post, $"/API/v2/Admin/Roles/{ExampleDataCreator.RoleName}", AccessTokenValidatorMiddleware.AdminTokenHeaderName, this.AdminToken);
            string answerOfTheClient = this.SendToTheAdministration(HttpMethod.Post, $"/API/v2/Admin/Clients/{ClientName}", AccessTokenValidatorMiddleware.AdminTokenHeaderName, this.AdminToken);
            this.ClientAdministrationToken = JsonDocument.Parse(answerOfTheClient).RootElement.GetProperty("clientAdministrationToken").GetString();
            this.SendToTheAdministration(HttpMethod.Post, $"/API/v2/Admin/Clients/{ClientName}/Roles/{ExampleDataCreator.RoleName}", AccessTokenValidatorMiddleware.AdminTokenHeaderName, this.AdminToken);
            string answerOfTheAccessToken = this.SendToTheAdministration(HttpMethod.Post, "/API/v2/ClientAdministration/AccessTokens?validForDays=1", AccessTokenValidatorMiddleware.ClientAdministrationTokenHeaderName, this.ClientAdministrationToken);
            this.AccessToken = JsonDocument.Parse(answerOfTheAccessToken).RootElement.GetProperty("accessToken").GetString();
        }

        /// <summary>
        /// Puts a token which this test-run knows into the file of the administrator of the started server and returns
        /// it.
        /// </summary>
        private static string SetTheTokenOfTheAdministrator(IAdminTokenService adminTokenService)
        {
            string token = Token.Create(Token.PrefixOfAnAdminToken);
            new CsvFile(adminTokenService.GetFile(), AdminTokenService.Header).WriteRows(new List<string[]>() { new string[] { Token.GetHash(token) } });
            return token;
        }

        /// <summary>
        /// Sends one request to the administration of the server and returns its answer.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when the server did not answer with a success, because a testcase which builds on a failed setup would fail somewhere else and for a reason which is not the actual one.</exception>
        private string SendToTheAdministration(HttpMethod method, string route, string headerName, string token)
        {
            using HttpClient client = new HttpClient();
            using HttpRequestMessage request = new HttpRequestMessage(method, $"{this.GetServerURL()}{route}");
            request.Headers.Add(headerName, token);
            HttpResponseMessage response = client.Send(request);
            string answer = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException($"The administration of the test-server answered \"{route}\" with the status-code {(int)response.StatusCode}: {answer}");
            }
            return answer;
        }

        /// <summary>
        /// States whether the server answers on the endpoint the testcases use.
        /// </summary>
        /// <remarks>
        /// The readiness is asked on the MCP-endpoint itself and not on a maintenance-route: what a testcase needs to
        /// know is whether that endpoint answers, and any other route could be up while it is not. "ping" is the method
        /// of the protocol which exists for exactly this question and which changes nothing.
        ///
        /// It is asked at the mcp-server which is declared as anonymous, because at this moment no access-token exists
        /// yet: the tokens are created over the api of the very server whose readiness is asked here.
        /// </remarks>
        private bool IsReady()
        {
            try
            {
                (HttpStatusCode statusCode, string _) = this.SendMCPMessageAndGetStatusCode(@"{""jsonrpc"":""2.0"",""id"":0,""method"":""ping""}", ExampleDataCreator.AnonymousUrlNamespace, null);
                return Enumerable.Range(200, 100).Contains((int)statusCode);
            }
            catch
            {
                return false;
            }
        }

        public string GetServerURL()
        {
            return $"http://127.0.0.1:{HTTP.DefaultPort}";
        }

        /// <summary>
        /// The endpoint of one mcp-server, over which every message of the MCP-protocol with it is exchanged.
        /// </summary>
        public string GetMCPEndpointURL(string urlNamespace)
        {
            return $"{this.GetServerURL()}/{urlNamespace}";
        }

        /// <summary>
        /// Sends one JSON-RPC-message to the MCP-endpoint and returns the answer.
        /// </summary>
        /// <remarks>
        /// The accept-header has to name both content-types: the transport answers as an event-stream, so a client
        /// which only accepts json gets rejected before the message is even read.
        /// </remarks>
        public string SendMCPMessage(string message)
        {
            (HttpStatusCode statusCode, string answer) = this.SendMCPMessageAndGetStatusCode(message, ExampleDataCreator.UrlNamespace, this.AccessToken);
            if (!Enumerable.Range(200, 100).Contains((int)statusCode))
            {
                throw new InvalidOperationException($"The server answered with the status-code {(int)statusCode}: {answer}");
            }
            return answer;
        }

        /// <summary>
        /// Sends one JSON-RPC-message and returns the answer together with the status-code it arrived with.
        /// </summary>
        /// <remarks>
        /// The status-code is part of what a testcase has to be able to check: a request whose tool-call failed is
        /// answered with 502, so a testcase of a failing tool can not use <see cref="SendMCPMessage"/>.
        /// </remarks>
        public (HttpStatusCode StatusCode, string Answer) SendMCPMessageAndGetStatusCode(string message)
        {
            return this.SendMCPMessageAndGetStatusCode(message, ExampleDataCreator.UrlNamespace, this.AccessToken);
        }

        /// <summary>
        /// Sends one JSON-RPC-message with the given access-token.
        /// </summary>
        /// <param name="accessToken">The token, or null to send no token at all.</param>
        public (HttpStatusCode StatusCode, string Answer) SendMCPMessageAndGetStatusCode(string message, string urlNamespace, string? accessToken)
        {
            using HttpClient client = new HttpClient();
            using StringContent content = new StringContent(message, Encoding.UTF8, "application/json");
            client.DefaultRequestHeaders.Add("Accept", "application/json, text/event-stream");
            if (accessToken != null)
            {
                client.DefaultRequestHeaders.Add("Authorization", $"Bearer {accessToken}");
            }
            HttpResponseMessage response = client.PostAsync(this.GetMCPEndpointURL(urlNamespace), content).GetAwaiter().GetResult();
            return (response.StatusCode, response.Content.ReadAsStringAsync().GetAwaiter().GetResult());
        }

        /// <summary>
        /// Returns the json-object of the answer of the MCP-endpoint.
        /// </summary>
        /// <remarks>
        /// The answer arrives as an event-stream, so the json is the part of the line which starts with "data: ".
        /// </remarks>
        public static string GetPayloadOfAnswer(string answer)
        {
            foreach (string line in answer.Split('\n'))
            {
                if (line.StartsWith("data: ", StringComparison.Ordinal))
                {
                    return line.Substring("data: ".Length).Trim();
                }
            }
            throw new InvalidOperationException($"The answer does not contain a data-line: {answer}");
        }

        public void Dispose()
        {
            if (this._Program.IsRunning)
            {
                // Stop blocks until the server has really stopped and the port is free again, so that the next test
                // which takes the lock finds it available.
                this._Program.Stop();
            }
            this._ExampleDataCreator.Dispose();
        }
    }
}
