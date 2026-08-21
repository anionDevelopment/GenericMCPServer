using GenericMCPServerBackend.Core.Constants;
using GenericMCPServerBackend.Core.Misc;
using GenericMCPServerBackend.Core.Services;
using GRYLibrary.Core.APIServer.Services.Interfaces;
using GRYLibrary.Core.APIServer.Settings.Configuration;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Linq;

namespace GenericMCPServerBackend.Core.Controller
{
    /// <summary>
    /// The routes with which a client administrates its own access-tokens.
    ///
    /// Every route of this controller needs the client-administration-token which the client received when an
    /// administrator created it. That check is not implemented here but in
    /// <see cref="AccessTokenValidatorMiddleware"/>, which also states which client is calling - so a client can only
    /// ever see and change its own access-tokens and can not even name another one.
    ///
    /// This exists so that whoever runs a client can exchange its tokens without an administrator: an administrator
    /// decides which clients exist and which roles they have, and that decision is not touched here.
    /// </summary>
    [ApiController]
    [Route(CodeUnitSpecificConstants.ClientAdministrationRoute)]
    public class ClientAdministrationController : ControllerBase
    {
        /// <summary>
        /// The amount of days a token is valid when the request states none. Every token ends; there is no endless one.
        /// </summary>
        private const int DefaultAmountOfDays = 365;

        private readonly IAccessTokenService _AccessTokenService;
        private readonly ITimeService _TimeService;

        public ClientAdministrationController(IAccessTokenService accessTokenService, ITimeService timeService)
        {
            this._AccessTokenService = accessTokenService;
            this._TimeService = timeService;
        }

        /// <summary>
        /// The client which is calling. The middleware which checked the token stated it, so it is always there when a
        /// request arrives here.
        /// </summary>
        private string GetClient()
        {
            return (string)this.HttpContext.Items[AccessTokenValidatorMiddleware.ClientInformationName];
        }

        /// <summary>
        /// The access-tokens of the own client. They contain the hash and never the token itself, because a token is
        /// readable exactly once, namely when it is created.
        /// </summary>
        [HttpGet]
        [Route("AccessTokens")]
        public IActionResult GetAccessTokens()
        {
            string client = this.GetClient();
            return this.Ok(this._AccessTokenService.GetAccessTokensOf(client).Select(accessToken => new
            {
                hash = accessToken.Hash,
                validUntil = accessToken.ValidUntil,
                isValid = accessToken.IsValidAt(this._TimeService.GetCurrentTimeInUTCAsDateTimeOffset().UtcDateTime),
            }));
        }

        /// <summary>
        /// Creates an access-token for the own client and answers with it.
        /// </summary>
        /// <param name="validForDays">The amount of days the token is valid.</param>
        /// <remarks>
        /// A client can hold several valid tokens at the same time, which is what makes an exchange possible without an
        /// interruption: the new token is created, whatever uses it is switched over, and only then the old one is
        /// removed.
        /// </remarks>
        [HttpPost]
        [Route("AccessTokens")]
        public IActionResult CreateAccessToken([FromQuery] int validForDays = DefaultAmountOfDays)
        {
            string client = this.GetClient();
            try
            {
                return this.Ok(new
                {
                    client,
                    accessToken = this._AccessTokenService.CreateAccessToken(client, validForDays),
                    validForDays,
                    hint = "This token is shown now and never again. It is sent as \"Authorization: Bearer <token>\" to an mcp-server which one of the roles of this client may use.",
                });
            }
            catch (ArgumentException exception)
            {
                return this.BadRequest(exception.Message);
            }
        }

        /// <summary>
        /// Removes one access-token of the own client. The token is stated by its hash, which the list of the tokens
        /// contains.
        /// </summary>
        [HttpDelete]
        [Route("AccessTokens/{hash}")]
        public IActionResult DeleteAccessToken(string hash)
        {
            string client = this.GetClient();
            if (!this._AccessTokenService.RemoveAccessToken(client, hash))
            {
                return this.NotFound($"This client has no access-token with the hash \"{hash}\".");
            }
            return this.NoContent();
        }
    }
}
