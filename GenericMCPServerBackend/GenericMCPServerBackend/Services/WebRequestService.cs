using GenericMCPServerBackend.Core.Model;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace GenericMCPServerBackend.Core.Services
{
    /// <summary>
    /// Runs the web-requests of a tool and of a resource of the type <see cref="ToolType.WebRequest"/>.
    ///
    /// Such a tool implements no script: the client states which address it wants, and the declaration states which
    /// addresses the tool allows. Everything which is not allowed by a rule of the declaration is denied - a tool
    /// without a rule can therefore reach nothing, which is the safe direction for a mistake in a declaration.
    ///
    /// Such a resource states no rules at all, because there is nothing to decide: a resource is read by its uri, and
    /// that uri - which the declaration states and not the client - is the address which is requested.
    /// </summary>
    public sealed class WebRequestService
    {
        /// <summary>
        /// The name of the parameter such a tool takes. It belongs to the mechanism and not to a declared tool: an
        /// administrator declares which addresses are allowed, the client states which of them it wants.
        /// </summary>
        public const string UrlParameterName = "url";

        /// <summary>The protocol every request uses. It is not part of an address and it can not be changed.</summary>
        public const string Protocol = "https";

        private const int TimeoutInSeconds = 60;

        /// <summary>
        /// The limit for the answer. A tool-result is read by a model, so an answer which is larger than this is not
        /// useful anymore, and cutting it silently would hand out a part of a document as if it were the document.
        /// </summary>
        private const int MaximalLengthOfBodyInCharacters = 1000000;

        /// <summary>
        /// One client for all requests: a client which is created per request would keep its connection in the
        /// operating-system after it was disposed and would run the machine out of sockets under load.
        /// </summary>
        private static readonly HttpClient Client = CreateClient();

        private readonly CredentialsService _CredentialsService;

        public WebRequestService(CredentialsService credentialsService)
        {
            this._CredentialsService = credentialsService;
        }

        private static HttpClient CreateClient()
        {
            // Redirects are not followed: the address which was checked against the rules would otherwise only be the
            // first of a chain, and the answer would come from an address which no rule ever allowed. A redirect is
            // handed to the client as what it is - an answer with a status-code of the 3xx-range.
            HttpClient result = new HttpClient(new HttpClientHandler() { AllowAutoRedirect = false })
            {
                Timeout = TimeSpan.FromSeconds(TimeoutInSeconds),
            };
            result.DefaultRequestHeaders.Add("User-Agent", "GenericMCPServer");
            return result;
        }

        /// <summary>
        /// Checks whether the tool allows the requested address and, if it does, requests it and returns the answer as
        /// a json-document with the status-code and the body.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when no address was given, the address is not usable, no rule of the tool allows it or the request failed.</exception>
        public string Run(ToolDefinition definition, IReadOnlyDictionary<string, string> arguments)
        {
            if (!arguments.TryGetValue(UrlParameterName, out string requestedAddress) || string.IsNullOrWhiteSpace(requestedAddress))
            {
                throw new InvalidOperationException($"The tool \"{definition.ToolName}\" needs the argument \"{UrlParameterName}\".");
            }
            string address = GetAddress(definition, requestedAddress);
            if (!IsAllowed(definition, address))
            {
                throw new InvalidOperationException($"The tool \"{definition.ToolName}\" does not allow a request to \"{address}\". A tool allows exactly the addresses which the rules of its declaration match.");
            }
            (int statusCode, string body) = this.Request(definition, address);
            return JsonSerializer.Serialize(new Dictionary<string, object>()
            {
                { "status-code", statusCode },
                { "body", body },
            });
        }

        /// <summary>
        /// Requests the address of the given resource and returns its content.
        ///
        /// Unlike a tool, a resource answers with the content itself and not with the status-code and the body: a
        /// client reads a resource to receive a document, and a document which is wrapped in something else is no
        /// document anymore. An answer which is no success is therefore a failure of the read and not a content, so
        /// that what a client receives is never the error-page of a foreign service.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when the address is not usable, the request failed or the answer is no success.</exception>
        public string ReadResource(ResourceDefinition definition)
        {
            string address = GetAddress(definition, definition.Uri);
            (int statusCode, string body) = this.Request(definition, address);
            if (statusCode < 200 || 299 < statusCode)
            {
                throw new InvalidOperationException($"The resource \"{definition.ResourceName}\" is not available: \"{address}\" answered with the status-code {statusCode}.");
            }
            return body;
        }

        /// <summary>
        /// Builds the headers of the request: every reference of the form "${&lt;name&gt;}" is replaced by the credential
        /// of the tool which carries that name. That a reference points at a declared credential was already checked
        /// while the declarations were read.
        /// </summary>
        /// <param name="credentialValues">The credentials of the tool, keyed by the name the declaration gave them.</param>
        internal static IDictionary<string, string> BuildHeaders(CallableDefinition definition, IReadOnlyDictionary<string, string> credentialValues)
        {
            Dictionary<string, string> result = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, string> header in definition.Headers)
            {
                string value = header.Value ?? string.Empty;
                foreach (KeyValuePair<string, string> credential in credentialValues)
                {
                    value = value.Replace("${" + credential.Key + "}", credential.Value, StringComparison.Ordinal);
                }
                result[header.Key] = value;
            }
            return result;
        }

        /// <summary>
        /// Turns what the client asked for into the address which is checked and requested.
        ///
        /// The result contains no protocol, because the protocol is not part of an address here: it is always
        /// <see cref="Protocol"/>. The address is taken from the parsed url and not from the text the client sent, so
        /// that the rules see exactly the host and the path which the request would use and not a text which looks like
        /// them.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when the address is not a usable https-address.</exception>
        private static string GetAddress(CallableDefinition definition, string requestedAddress)
        {
            string addressWithoutProtocol = requestedAddress.Trim();
            string protocolPrefix = $"{Protocol}://";
            if (addressWithoutProtocol.StartsWith(protocolPrefix, StringComparison.OrdinalIgnoreCase))
            {
                addressWithoutProtocol = addressWithoutProtocol.Substring(protocolPrefix.Length);
            }
            else if (addressWithoutProtocol.Contains("://", StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"The {definition.Kind} \"{definition.Name}\" can only request addresses of the protocol \"{Protocol}\", but \"{requestedAddress}\" states another one.");
            }
            if (!Uri.TryCreate(protocolPrefix + addressWithoutProtocol, UriKind.Absolute, out Uri url))
            {
                throw new InvalidOperationException($"The {definition.Kind} \"{definition.Name}\" can not request \"{requestedAddress}\", because it is not a usable address.");
            }
            if (!string.IsNullOrEmpty(url.UserInfo))
            {
                // Such an address would be matched by the rules without its credentials while the request would send
                // them, so what is allowed and what happens would not be the same thing anymore.
                throw new InvalidOperationException($"The {definition.Kind} \"{definition.Name}\" can not request \"{requestedAddress}\", because an address with credentials is not supported.");
            }
            string port = url.IsDefaultPort ? string.Empty : $":{url.Port}";
            return $"{url.Host}{port}{url.PathAndQuery}";
        }

        /// <summary>
        /// Decides whether a rule of the tool allows the address.
        ///
        /// Every rule has to match the whole address: a rule is anchored at both ends here, so that a rule like
        /// "example\.com/.*" can not be satisfied by an address which only contains it somewhere (as
        /// "evil.com/?x=example.com/" would).
        /// </summary>
        private static bool IsAllowed(ToolDefinition definition, string address)
        {
            foreach (string rule in definition.AllowedUrls)
            {
                if (Regex.IsMatch(address, "^" + rule + "$"))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Requests the given address and returns what came back. What is made of that answer is decided by the
        /// caller, because a tool hands over the status-code together with the body while a resource is the body.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when the request failed or the answer is too large.</exception>
        private (int StatusCode, string Body) Request(CallableDefinition definition, string address)
        {
            try
            {
                using (HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, $"{Protocol}://{address}"))
                using (HttpResponseMessage response = this.Send(definition, request))
                {
                    return ((int)response.StatusCode, ReadBody(definition, response));
                }
            }
            catch (HttpRequestException exception)
            {
                throw new InvalidOperationException($"The {definition.Kind} \"{definition.Name}\" could not request \"{address}\": {exception.Message}", exception);
            }
            catch (TaskCanceledException exception)
            {
                throw new InvalidOperationException($"The {definition.Kind} \"{definition.Name}\" did not get an answer from \"{address}\" within {TimeoutInSeconds} seconds.", exception);
            }
        }

        /// <summary>
        /// Sends the request with the headers the declaration states. They are added without validation, because a
        /// header which a foreign service expects is not necessarily one which this codeunit knows.
        /// </summary>
        private HttpResponseMessage Send(CallableDefinition definition, HttpRequestMessage request)
        {
            IReadOnlyDictionary<string, string> credentialValues = definition.Credentials.ToDictionary(credential => credential.Key, credential => this._CredentialsService.GetValue(credential.Value), StringComparer.Ordinal);
            foreach (KeyValuePair<string, string> header in BuildHeaders(definition, credentialValues))
            {
                request.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
            return Client.Send(request);
        }

        /// <exception cref="InvalidOperationException">Thrown when the answer is larger than <see cref="MaximalLengthOfBodyInCharacters"/>.</exception>
        private static string ReadBody(CallableDefinition definition, HttpResponseMessage response)
        {
            using (Stream stream = response.Content.ReadAsStream())
            using (StreamReader reader = new StreamReader(stream, Encoding.UTF8))
            {
                StringBuilder result = new StringBuilder();
                char[] buffer = new char[4096];
                int readCharacters;
                while (0 < (readCharacters = reader.Read(buffer, 0, buffer.Length)))
                {
                    result.Append(buffer, 0, readCharacters);
                    if (MaximalLengthOfBodyInCharacters < result.Length)
                    {
                        throw new InvalidOperationException($"The answer of the {definition.Kind} \"{definition.Name}\" is larger than {MaximalLengthOfBodyInCharacters} characters.");
                    }
                }
                return result.ToString();
            }
        }
    }
}
