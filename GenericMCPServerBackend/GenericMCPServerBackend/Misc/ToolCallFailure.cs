using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using System;

namespace GenericMCPServerBackend.Core.Misc
{
    /// <summary>
    /// Remembers that a tool-call of the current request failed.
    ///
    /// The tool is run deep inside the SDK while the status-code of the answer is decided by
    /// <see cref="ToolCallFailureStatusCodeMiddleware"/>, so the two need one place to meet. That place is the
    /// http-request, because it is the thing both of them belong to - a static field would be shared by all requests
    /// which are answered at the same time.
    /// </summary>
    internal static class ToolCallFailure
    {
        private const string ItemKey = "GenericMCPServerBackend.ToolCallFailed";

        /// <summary>
        /// Reports that a tool-call failed. The services of the message are used to reach the http-request, because a
        /// tool knows the request it is running in only through them.
        /// </summary>
        internal static void Report(IServiceProvider services)
        {
            HttpContext httpContext = services?.GetService<IHttpContextAccessor>()?.HttpContext;
            if (httpContext != null)
            {
                httpContext.Items[ItemKey] = true;
            }
        }

        /// <summary>
        /// Whether a tool-call of this request failed. One failed call is enough: a request whose answer contains a
        /// failure is not an answer which worked.
        /// </summary>
        internal static bool WasReported(HttpContext httpContext)
        {
            return httpContext.Items.ContainsKey(ItemKey);
        }
    }
}
