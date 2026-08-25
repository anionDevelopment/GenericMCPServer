using GenericMCPServerBackend.Core.Constants;
using GenericMCPServerBackend.Core.Services;
using GRYLibrary.Core.APIServer.Settings.Configuration;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;

namespace GenericMCPServerBackend.Core.Controller
{
    /// <summary>
    /// The routes with which an administrator manages the clients and the roles.
    ///
    /// Every route of this controller needs the admin-token of the server, which is stated in its configuration-file
    /// and which is generated when that file is written for the first time. It is deliberately a token of its own and
    /// not one of the clients: whoever administrates the server is not one of the clients which use it.
    ///
    /// That check is not implemented here but in <see cref="Misc.AccessTokenValidatorMiddleware"/>, which decides for
    /// every route of this server who may reach it. A request which arrives here therefore already carried the
    /// admin-token. What each route does is decided by <see cref="IBusinessLogicService"/>; this controller only maps
    /// its result to a response.
    /// </summary>
    [ApiController]
    [Route(CodeUnitSpecificConstants.AdminRoute)]
    public class AdminController : ControllerBase
    {
        private readonly IBusinessLogicService _BusinessLogicService;

        public AdminController(IBusinessLogicService businessLogicService)
        {
            this._BusinessLogicService = businessLogicService;
        }

        /// <summary>The clients which exist.</summary>
        [HttpGet]
        [Route("Clients")]
        public IActionResult GetClients()
        {
            return this.Ok(this._BusinessLogicService.GetClients().Select(overview => new
            {
                client = overview.Client,
                roles = overview.Roles,
                amountOfAccessTokens = overview.AmountOfAccessTokens,
            }));
        }

        /// <summary>
        /// Creates a client and answers with the token with which that client administrates its own access-tokens.
        /// </summary>
        /// <remarks>
        /// The token is part of this answer and is never readable again, because only its hash is stored. It is handed
        /// to whoever runs that client.
        /// </remarks>
        [HttpPost]
        [Route("Clients/{client}")]
        public IActionResult CreateClient(string client)
        {
            try
            {
                return this.Ok(new
                {
                    client,
                    clientAdministrationToken = this._BusinessLogicService.CreateClient(client),
                    hint = "This token is shown now and never again. Hand it to whoever runs this client; it administrates the access-tokens of this client and nothing else.",
                });
            }
            catch (ArgumentException exception)
            {
                return this.BadRequest(exception.Message);
            }
            catch (InvalidOperationException exception)
            {
                return this.Conflict(exception.Message);
            }
        }

        /// <summary>
        /// Creates a new client-administration-token for a client and answers with it.
        /// </summary>
        /// <remarks>
        /// This is what an administrator does when the token of a client has to be exchanged - because it leaked,
        /// because the person who held it left, or simply because it is old. The previous token stops working at once,
        /// so whoever runs that client has to receive the new one. The access-tokens of the client stay as they are:
        /// they are what the client uses, and they are not touched by a change of who may administrate it.
        /// </remarks>
        [HttpPost]
        [Route("Clients/{client}/AdministrationToken")]
        public IActionResult RenewClientAdministrationToken(string client)
        {
            try
            {
                return this.Ok(new
                {
                    client,
                    clientAdministrationToken = this._BusinessLogicService.RenewClientAdministrationToken(client),
                    hint = "This token is shown now and never again, and the previous one does not work anymore. Hand it to whoever runs this client.",
                });
            }
            catch (InvalidOperationException exception)
            {
                return this.NotFound(exception.Message);
            }
        }

        /// <summary>
        /// Removes a client together with its access-tokens and its roles.
        /// </summary>
        [HttpDelete]
        [Route("Clients/{client}")]
        public IActionResult DeleteClient(string client)
        {
            if (!this._BusinessLogicService.DeleteClient(client))
            {
                return this.NotFound($"A client with the name \"{client}\" does not exist.");
            }
            return this.NoContent();
        }

        /// <summary>The roles which exist.</summary>
        [HttpGet]
        [Route("Roles")]
        public IActionResult GetRoles()
        {
            return this.Ok(this._BusinessLogicService.GetRoles());
        }

        /// <summary>
        /// Creates a role. Which mcp-server a role may use is stated in the tool-definitions-file and not here.
        /// </summary>
        [HttpPost]
        [Route("Roles/{role}")]
        public IActionResult CreateRole(string role)
        {
            try
            {
                if (!this._BusinessLogicService.CreateRole(role))
                {
                    return this.Conflict($"The role \"{role}\" exists already.");
                }
                return this.Ok(new { role });
            }
            catch (ArgumentException exception)
            {
                return this.BadRequest(exception.Message);
            }
        }

        /// <summary>
        /// Removes a role and every assignment of it.
        /// </summary>
        [HttpDelete]
        [Route("Roles/{role}")]
        public IActionResult DeleteRole(string role)
        {
            if (!this._BusinessLogicService.DeleteRole(role))
            {
                return this.NotFound($"The role \"{role}\" does not exist.");
            }
            return this.NoContent();
        }

        /// <summary>The roles of one client.</summary>
        [HttpGet]
        [Route("Clients/{client}/Roles")]
        public IActionResult GetRolesOfClient(string client)
        {
            IReadOnlyCollection<string> roles = this._BusinessLogicService.GetRolesOfClient(client);
            if (roles == null)
            {
                return this.NotFound($"A client with the name \"{client}\" does not exist.");
            }
            return this.Ok(roles);
        }

        /// <summary>Puts a client into a role.</summary>
        [HttpPost]
        [Route("Clients/{client}/Roles/{role}")]
        public IActionResult AssignRole(string client, string role)
        {
            try
            {
                if (!this._BusinessLogicService.AssignRole(client, role))
                {
                    return this.Conflict($"The client \"{client}\" is in the role \"{role}\" already.");
                }
                return this.Ok(new { client, role });
            }
            catch (ArgumentException exception)
            {
                return this.BadRequest(exception.Message);
            }
            catch (InvalidOperationException exception)
            {
                return this.NotFound(exception.Message);
            }
        }

        /// <summary>Removes a client from a role.</summary>
        [HttpDelete]
        [Route("Clients/{client}/Roles/{role}")]
        public IActionResult RemoveRoleOfClient(string client, string role)
        {
            if (!this._BusinessLogicService.RemoveRoleOfClient(client, role))
            {
                return this.NotFound($"The client \"{client}\" is not in the role \"{role}\".");
            }
            return this.NoContent();
        }
    }
}
