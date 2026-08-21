using GenericMCPServerBackend.Core.Configuration;
using GenericMCPServerBackend.Core.Constants;
using GenericMCPServerBackend.Core.Model;
using GenericMCPServerBackend.Core.Services;
using GRYLibrary.Core.APIServer.Mid.M01APIK;
using GRYLibrary.Core.APIServer.Services.Logger;
using GRYLibrary.Core.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace GenericMCPServerBackend.Core.Misc
{
    /// <summary>
    /// Decides who may reach what.
    ///
    /// This is the one place which answers that question, for all three kinds of caller this server knows:
    ///
    /// - an mcp-server is reachable when it is declared as anonymous, or when the request carries an access-token of a
    ///   client which is in one of the roles that mcp-server declares,
    /// - the routes of the administration are reachable with the admin-token which the configuration-file of the
    ///   server states,
    /// - the routes with which a client administrates itself are reachable with the client-administration-token of a
    ///   client, and which client is calling follows from that token.
    ///
    /// The controllers therefore contain no check of their own: a check which lives in a controller is a check which
    /// the next controller can forget.
    ///
    /// It builds on the api-key-middleware of GRYLibrary, which brings the reading of the key, the check of its
    /// minimal length and the logging of a shortened key along. Two things are answered differently here: the
    /// base-class decides by the attributes of a controller-action whether a request needs a key, which does not work
    /// for the route of an mcp-server (it is an endpoint of the SDK and no controller-action), and a request which may
    /// not pass is answered with 401 and not with 403 - whether the token was missing, unknown, expired or belongs to
    /// somebody without a fitting role is nothing a caller may learn from the answer.
    /// </summary>
    internal sealed class AccessTokenValidatorMiddleware : APIKeyValidatorMiddleware
    {
        /// <summary>
        /// Under this name the client of the request is remembered, so that the routes with which a client
        /// administrates itself know which client is calling without asking again.
        /// </summary>
        internal const string ClientInformationName = "Client";

        /// <summary>The way a MCP-client sends its access-token.</summary>
        private const string AuthorizationHeaderName = "Authorization";

        private const string BearerPrefix = "Bearer ";

        /// <summary>The header the admin-token is sent in.</summary>
        internal const string AdminTokenHeaderName = "X-Admin-Token";

        /// <summary>The header the client-administration-token is sent in.</summary>
        internal const string ClientAdministrationTokenHeaderName = "X-Client-Administration-Token";

        private readonly IAccessTokenService _AccessTokenService;

        private readonly IBusinessLogicService _BusinessLogicService;

        private readonly IRolesService _RolesService;

        private readonly IClientService _ClientService;

        private readonly IAdminTokenService _AdminTokenService;

        // The logger is taken from the service which holds it, the way the middlewares of GRYLibrary do it: the logger
        // itself is not part of the container.
        public AccessTokenValidatorMiddleware(RequestDelegate next, IAccessTokenService accessTokenService, IRolesService rolesService, IClientService clientService, IBusinessLogicService businessLogicService, IAdminTokenService adminTokenService, IServerLog logger)
            : base(next, new AccessTokenValidatorConfiguration(), logger.Logger)
        {
            this._AccessTokenService = accessTokenService;
            this._RolesService = rolesService;
            this._ClientService = clientService;
            this._BusinessLogicService = businessLogicService;
            this._AdminTokenService = adminTokenService;
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

        /// <summary>
        /// Decides whether the access-token belongs to a client which may use the mcp-server of this request.
        ///
        /// The token says which client is calling; whether that client may use this mcp-server is decided by its
        /// roles. A client which is in none of the declared roles is a known client without an access here.
        /// </summary>
        public override bool APIKeyIsAuthorized(string apiKey, HttpContext context)
        {
            MCPServerDefinition mcpServer = this._BusinessLogicService.GetMCPServerOfRoute(context.Request.Path);
            if (mcpServer == null || !this._AccessTokenService.TryGetClient(apiKey, out string client))
            {
                return false;
            }
            context.Items[ClientInformationName] = client;
            IReadOnlyCollection<string> rolesOfTheClient = this._RolesService.GetRolesOf(client);
            foreach (string role in mcpServer.AuthorizedRoles)
            {
                if (rolesOfTheClient.Contains(role, StringComparer.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }

        public override Task Invoke(HttpContext context)
        {
            if (context.Request.Path.StartsWithSegments(CodeUnitSpecificConstants.AdminRoute))
            {
                return this.PassIf(context, this.RequestIsFromAnAdministrator(context));
            }
            if (context.Request.Path.StartsWithSegments(CodeUnitSpecificConstants.ClientAdministrationRoute))
            {
                return this.PassIf(context, this.RequestIsFromAClientWhichAdministratesItself(context));
            }
            MCPServerDefinition mcpServer = this._BusinessLogicService.GetMCPServerOfRoute(context.Request.Path);
            if (mcpServer == null)
            {
                // Everything else - above all the health-check, which the image checks itself with (see the HEALTHCHECK
                // of the Dockerfile) - stays reachable without a token.
                return this._Next(context);
            }
            // An mcp-server is either one which everybody may use or one which only its own clients may use. That is
            // decided per mcp-server and not per tool, so the route already answers the question.
            return this.PassIf(context, mcpServer.Anonymous || this.IsAuthorized(context));
        }

        private Task PassIf(HttpContext context, bool requestMayPass)
        {
            if (requestMayPass)
            {
                return this._Next(context);
            }
            throw new BadRequestException(StatusCodes.Status401Unauthorized);
        }

        /// <summary>
        /// States whether the request carries the admin-token of this server.
        /// </summary>
        /// <remarks>
        /// The token of the request is hashed and compared to the hash which was stored when the token was created:
        /// the token itself is nowhere on this server. The comparison runs in a time which does not depend on how many
        /// characters are equal, so the answer says nothing about how close a wrong token was to the right one.
        /// </remarks>
        private bool RequestIsFromAnAdministrator(HttpContext context)
        {
            return this._AdminTokenService.Matches(context.Request.Headers[AdminTokenHeaderName].ToString());
        }

        /// <summary>
        /// States whether the request carries the administration-token of a client and remembers which client that is.
        /// </summary>
        private bool RequestIsFromAClientWhichAdministratesItself(HttpContext context)
        {
            if (this._ClientService.TryGetClientOfAdministrationToken(context.Request.Headers[ClientAdministrationTokenHeaderName].ToString(), out string client))
            {
                context.Items[ClientInformationName] = client;
                return true;
            }
            return false;
        }
    }
}
