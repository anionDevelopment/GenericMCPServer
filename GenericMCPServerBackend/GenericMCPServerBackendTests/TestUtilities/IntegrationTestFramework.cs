using GRYLibrary.Core.APIServer.Settings.Configuration;
using GenericMCPServerBackend.Core;
using GenericMCPServerBackend.Core.Services;
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
        private readonly ExampleDataCreator _ExampleDataCreator;

        /// <summary>The tools the started server offers come from this example-data.</summary>
        public ExampleDataCreator ExampleData => this._ExampleDataCreator;

        /// <summary>The name of the client the testcases reach the server as.</summary>
        public const string ClientName = "integration-test";

        /// <summary>
        /// The access-token of that client. Every request needs one, so it is created before the server is started.
        /// </summary>
        public string AccessToken { get; }

        public IntegrationTestFramework()
        {
            // The port is fixed, so a port which is occupied here is held by something outside the test-run (a running
            // server or a second test-run). Failing with that diagnosis is more useful than the "could not start
            // service" which would follow otherwise.
            if (GRYLibrary.Core.Misc.Utilities.PortIsAvailable("127.0.0.1", HTTP.DefaultPort))
            {
                throw new InvalidOperationException($"The port {HTTP.DefaultPort} which the test-server uses is already in use by a process outside the test-run.");
            }
            this._ExampleDataCreator = new ExampleDataCreator();
            this.AccessToken = new AccessTokenService(this._ExampleDataCreator.AccessTokensFile).CreateAccessToken(ClientName, 1);
            // The client is put into the role which the mcp-server of the example-data declares, the way an
            // administrator writes it into the roles-file.
            System.IO.File.WriteAllLines(this._ExampleDataCreator.RolesFile, new string[] { "client;role", $"{ClientName};{ExampleDataCreator.RoleName}" });
            this._Program = new Program()
            {
                // The server runs in its own thread and blocks there, so it stays up until Stop is called. Running it
                // asynchronously instead would let MainImplementation return immediately, which ends the run.
                RunAsync = false,
                // The server has to listen on every ip: with the domain-only binding the testcases would have to
                // resolve the configured domain, while they address the server under 127.0.0.1.
                ListenOnEveryIP = true,
            };
            Thread thread = new Thread(() => this._Program.MainImplementation(new string[]
            {
                // No "--RealRun true": without it the server runs in its test-run-mode, which binds plain http on the
                // default-port and therefore needs no certificate.
                // The initialization-state-route is what the readiness-probe below asks, so it has to be available.
                $"--{nameof(GenericMCPServerBackend.Core.Configuration.CommandlineParameter.InitialEnableEndpointInitializationStateValue)}", "true",
                $"--{nameof(GenericMCPServerBackend.Core.Configuration.CommandlineParameter.InitialToolDefinitionsFile)}", this._ExampleDataCreator.ToolDefinitionsFile,
                // The virtual environment of the example-tool belongs to the example-data, so that the test-run leaves
                // nothing behind in the data-folder of the server.
                $"--{nameof(GenericMCPServerBackend.Core.Configuration.CommandlineParameter.InitialVirtualEnvironmentsFolder)}", this._ExampleDataCreator.VirtualEnvironmentsFolder,
                $"--{nameof(GenericMCPServerBackend.Core.Configuration.CommandlineParameter.InitialAccessTokensFile)}", this._ExampleDataCreator.AccessTokensFile,
                $"--{nameof(GenericMCPServerBackend.Core.Configuration.CommandlineParameter.InitialRolesFile)}", this._ExampleDataCreator.RolesFile,
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
        }

        /// <summary>
        /// States whether the server answers on the endpoint the testcases use.
        /// </summary>
        /// <remarks>
        /// The readiness is asked on the MCP-endpoint itself and not on a maintenance-route: what a testcase needs to
        /// know is whether that endpoint answers, and any other route could be up while it is not. "ping" is the method
        /// of the protocol which exists for exactly this question and which changes nothing.
        /// </remarks>
        private bool IsReady()
        {
            try
            {
                this.SendMCPMessage(@"{""jsonrpc"":""2.0"",""id"":0,""method"":""ping""}");
                return true;
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
