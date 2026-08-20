using GenericMCPServerBackend.Core.Configuration;
using GenericMCPServerBackend.Core.Model;
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
            MCPServerDefinition mcpServer = this._BusinessLogicService.GetMCPServerOfRoute(context.Request.Path);
            if (mcpServer != null && this._AccessTokenService.TryGetClient(apiKey, mcpServer.UrlNamespace, out string client))
            {
                context.Items[ClientInformationName] = client;
                return true;
            }
            return false;
        }

        public override Task Invoke(HttpContext context)
        {
            // Only the route of an mcp-server is protected. Everything else - above all the health-check, which the
            // image checks itself with (see the HEALTHCHECK of the Dockerfile) - stays reachable without a token.
            MCPServerDefinition mcpServer = this._BusinessLogicService.GetMCPServerOfRoute(context.Request.Path);
            if (mcpServer == null)
            {
                return this._Next(context);
            }
            // An mcp-server is either one which everybody may use or one which only its own clients may use. That is
            // decided per mcp-server and not per tool, so the route already answers the question.
            if (mcpServer.Anonymous || this.IsAuthorized(context))
            {
                return this._Next(context);
            }
            throw new BadRequestException(StatusCodes.Status401Unauthorized);
        }

    }
}
