using GenericMCPServerBackend.Core.Configuration;
using GenericMCPServerBackend.Core.Constants;
using GenericMCPServerBackend.Core.Services;
using GRYLibrary.Core.APIServer.Mid.M01APIK;
using GRYLibrary.Core.Exceptions;
using GRYLibrary.Core.Logging.GRYLogger;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using System;
using System.Linq;
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

        public AccessTokenValidatorMiddleware(RequestDelegate next, IAccessTokenService accessTokenService, IGRYLog log)
            : base(next, new AccessTokenValidatorConfiguration(), log)
        {
            this._AccessTokenService = accessTokenService;
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

        public override Task Invoke(HttpContext context)
        {
            // Only the route of the protocol is protected. The health-check has to stay reachable without a token,
            // because the image checks itself with it (see the HEALTHCHECK of the Dockerfile).
            if (!context.Request.Path.StartsWithSegments(CodeUnitSpecificConstants.MCPRoute))
            {
                return this._Next(context);
            }
            if (!this.IsAuthorized(context))
            {
                throw new BadRequestException(StatusCodes.Status401Unauthorized);
            }
            return this._Next(context);
        }
    }
}
