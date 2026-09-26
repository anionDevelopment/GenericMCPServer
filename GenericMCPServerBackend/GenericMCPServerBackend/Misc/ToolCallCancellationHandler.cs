using Microsoft.AspNetCore.Http;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace GenericMCPServerBackend.Core.Misc
{
    /// <summary>
    /// Stops the tool-call which a client cancelled ("notifications/cancelled" of the MCP-protocol).
    ///
    /// One instance belongs to one http-request, because that request is what states who is cancelling: the mcp-server
    /// its route belongs to and the client its access-token belongs to. A cancellation can therefore only stop a call
    /// of that same mcp-server and of that same client, although the notification itself states nothing but a
    /// request-id - an id which every client chooses for itself and which two of them can both use.
    ///
    /// Only a tool-call is stopped. A read of a resource, a prompt and a completion are answered before a client could
    /// send anything else at all in the common case, while a tool-call is the one which runs for as long as the program
    /// behind it runs; and because the notification names a request-id and not what that request was about, a
    /// cancellation which names one of the others simply stops nothing.
    /// </summary>
    public sealed class ToolCallCancellationHandler
    {
        private readonly RunningToolCalls _RunningToolCalls;

        /// <summary>
        /// The mcp-server the request belongs to. A request-id is unambiguous within the client which chose it and
        /// says nothing about the mcp-server, so the namespace is part of what identifies the call which is meant.
        /// </summary>
        private readonly string _UrlNamespace;

        /// <summary>The client which is cancelling, or null when the mcp-server is one which everybody may use.</summary>
        private readonly string _Client;

        public ToolCallCancellationHandler(RunningToolCalls runningToolCalls, string urlNamespace, string client)
        {
            this._RunningToolCalls = runningToolCalls;
            this._UrlNamespace = urlNamespace;
            this._Client = client;
        }

        /// <summary>
        /// The notification-handlers which one request of the given mcp-server gets.
        ///
        /// They are built per request and not once for the whole process, because the request is what states who is
        /// cancelling; everything they need beyond that is the registry of the running calls, which is the one thing
        /// two http-requests share.
        /// </summary>
        /// <param name="httpContext">The request the notification arrived with.</param>
        public static IEnumerable<KeyValuePair<string, Func<JsonRpcNotification, CancellationToken, ValueTask>>> CreateHandlersFor(RunningToolCalls runningToolCalls, string urlNamespace, HttpContext httpContext)
        {
            ToolCallCancellationHandler handler = new ToolCallCancellationHandler(runningToolCalls, urlNamespace, AccessTokenValidatorMiddleware.GetClientOf(httpContext));
            return new List<KeyValuePair<string, Func<JsonRpcNotification, CancellationToken, ValueTask>>>()
            {
                new KeyValuePair<string, Func<JsonRpcNotification, CancellationToken, ValueTask>>(NotificationMethods.CancelledNotification, handler.HandleAsync),
            };
        }

        /// <summary>
        /// Cancels the tool-call which the notification names.
        ///
        /// A notification which names a call that is not running is no failure and is answered with nothing: a client
        /// cancels a call it does not get an answer for yet, so a call which ended in the meantime is exactly the
        /// normal case of a cancellation which arrives too late.
        /// </summary>
        public ValueTask HandleAsync(JsonRpcNotification notification, CancellationToken cancellationToken)
        {
            if (notification.Params == null)
            {
                // A notification which carries nothing names no call, so there is nothing to stop.
                return default;
            }
            CancelledNotificationParams parameters = notification.Params.Deserialize<CancelledNotificationParams>(McpJsonUtilities.DefaultOptions);
            this._RunningToolCalls.Cancel(new ToolCallIdentity(this._UrlNamespace, this._Client, parameters.RequestId));
            return default;
        }
    }
}
