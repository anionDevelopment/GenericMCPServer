using GenericMCPServerBackend.Core.Configuration;
using GenericMCPServerBackend.Core.Constants;
using GenericMCPServerBackend.Core.Services;
using GRYLibrary.Core.APIServer.Mid.M01APIK;
using GRYLibrary.Core.APIServer.Services.Logger;
using GRYLibrary.Core.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Primitives;
using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace GenericMCPServerBackend.Core.Misc
{
    /// <summary>
    /// Lets only a client through which sends a valid access-token.
    ///
    /// It builds on the api-key-middleware of GRYLibrary, which brings the reading of the key, the check of its
    /// minimal length and the logging of a shortened key along. Two things are answered differently here:
    ///
    /// The base-class decides by the attributes of a controller-action whether a request needs a key. The route of the
    /// MCP-protocol is no controller-action but an endpoint of the SDK, so which route needs a token is decided here.
    ///
    /// A request without a usable token is answered with 401 and not with 403: the caller did not send something which
    /// is not enough, it sent nothing which could be checked at all.
    /// </summary>
    internal sealed class AccessTokenValidatorMiddleware : APIKeyValidatorMiddleware
    {
        /// <summary>
        /// Under this name the client of the request is remembered. Whoever the caller is belongs to the request, and
        /// a later decision (which client may use which tool) needs it.
        /// </summary>
        internal const string ClientInformationName = "Client";

        /// <summary>The way a MCP-client sends its token.</summary>
        private const string AuthorizationHeaderName = "Authorization";

        private const string BearerPrefix = "Bearer ";

        private readonly IAccessTokenService _AccessTokenService;

        private readonly IBusinessLogicService _BusinessLogicService;

        // The logger is taken from the service which holds it, the way the middlewares of GRYLibrary do it: the logger
        // itself is not part of the container.
        public AccessTokenValidatorMiddleware(RequestDelegate next, IAccessTokenService accessTokenService, IBusinessLogicService businessLogicService, IServerLog logger)
            : base(next, new AccessTokenValidatorConfiguration(), logger.Logger)
        {
            this._AccessTokenService = accessTokenService;
            this._BusinessLogicService = businessLogicService;
        }

        /// <summary>
        /// Reads the token of the request. A MCP-client sends it as "Authorization: Bearer &lt;token&gt;", which is how
        /// the protocol is used; the header of GRYLibrary stays possible as well, so a client which is configured like
        /// the other products of this family also works.
        /// </summary>
        public override (bool provided, string apiKey) TryGetAPIKey(HttpContext context)
        {
            if (context.Request.Headers.TryGetValue(AuthorizationHeaderName, out StringValues values) && values.Count == 1)
            {
                string value = values.First() ?? string.Empty;
                if (value.StartsWith(BearerPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    return (true, value.Substring(BearerPrefix.Length).Trim());
                }
            }
            return base.TryGetAPIKey(context);
        }

        public override bool APIKeyIsAuthorized(string apiKey, HttpContext context)
        {
            if (this._AccessTokenService.TryGetClient(apiKey, out string client))
            {
                context.Items[ClientInformationName] = client;
                return true;
            }
            return false;
        }

        public override async Task Invoke(HttpContext context)
        {
            // Only the route of the protocol is protected. The health-check has to stay reachable without a token,
            // because the image checks itself with it (see the HEALTHCHECK of the Dockerfile).
            if (!context.Request.Path.StartsWithSegments(CodeUnitSpecificConstants.MCPRoute))
            {
                await this._Next(context);
                return;
            }
            if (this.IsAuthorized(context) || await this.IsAllowedWithoutAnAccessTokenAsync(context))
            {
                await this._Next(context);
                return;
            }
            throw new BadRequestException(StatusCodes.Status401Unauthorized);
        }

        /// <summary>
        /// States whether the request may be answered although it carries no valid token.
        ///
        /// A tool which is declared as anonymous is usable by everybody, so a client has to be able to reach it: it
        /// has to be able to start the protocol, to see that tool and to call it. Everything else stays behind the
        /// token - what an anonymous caller sees in the list is decided where the list is built, so a tool which is
        /// not anonymous is not even visible.
        /// </summary>
        private async Task<bool> IsAllowedWithoutAnAccessTokenAsync(HttpContext context)
        {
            (string method, string toolName) = await ReadMessageAsync(context);
            if (method == null)
            {
                return false;
            }
            if (method == "initialize" || method == "ping" || method == "tools/list" || method.StartsWith("notifications/", StringComparison.Ordinal))
            {
                return true;
            }
            if (method == "tools/call")
            {
                return this._BusinessLogicService.ToolIsAnonymous(toolName);
            }
            return false;
        }

        /// <summary>
        /// Reads which method the message asks for and, if it is a call of a tool, which tool it asks for.
        ///
        /// The body is buffered before it is read, because the SDK reads it afterwards as well. A message which can
        /// not be read at all yields nothing, which makes the request one which needs a token - the safe direction.
        /// </summary>
        private static async Task<(string Method, string ToolName)> ReadMessageAsync(HttpContext context)
        {
            if (!HttpMethods.IsPost(context.Request.Method))
            {
                return (null, null);
            }
            context.Request.EnableBuffering();
            try
            {
                using (JsonDocument document = await JsonDocument.ParseAsync(context.Request.Body))
                {
                    if (document.RootElement.ValueKind != JsonValueKind.Object || !document.RootElement.TryGetProperty("method", out JsonElement method) || method.ValueKind != JsonValueKind.String)
                    {
                        return (null, null);
                    }
                    string toolName = null;
                    if (document.RootElement.TryGetProperty("params", out JsonElement parameters) && parameters.ValueKind == JsonValueKind.Object && parameters.TryGetProperty("name", out JsonElement name) && name.ValueKind == JsonValueKind.String)
                    {
                        toolName = name.GetString();
                    }
                    return (method.GetString(), toolName);
                }
            }
            catch (JsonException)
            {
                return (null, null);
            }
            finally
            {
                context.Request.Body.Position = 0;
            }
        }

        /// <summary>
        /// States whether the request which is currently answered belongs to a client which authenticated itself.
        /// </summary>
        internal static bool RequestIsAuthenticated(IServiceProvider services)
        {
            HttpContext httpContext = services?.GetService<IHttpContextAccessor>()?.HttpContext;
            return httpContext != null && httpContext.Items.ContainsKey(ClientInformationName);
        }
    }
}
