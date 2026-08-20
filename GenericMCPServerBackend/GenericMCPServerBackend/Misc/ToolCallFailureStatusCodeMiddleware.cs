using GenericMCPServerBackend.Core.Constants;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using System.IO;
using System.Threading.Tasks;

namespace GenericMCPServerBackend.Core.Misc
{
    /// <summary>
    /// Answers a request whose tool-call failed with the status-code 502.
    ///
    /// A tool is run by another program (a script or a foreign server), so this codeunit is a gateway for it: if that
    /// program fails, the failure is one of the gateway and not one of the caller, which is what 502 says. The answer
    /// itself is not changed - it still contains the message of the failure, so a client which reads the body learns
    /// what went wrong.
    ///
    /// The status-code has to be decided after the SDK wrote its answer, so the answer is collected here first and
    /// handed to the client afterwards. Collecting it is done by replacing the body-feature and not only the
    /// body-stream, because otherwise the SDK could send the head of the answer - and with it the status-code - before
    /// the tool ran.
    /// </summary>
    internal sealed class ToolCallFailureStatusCodeMiddleware
    {
        private const int StatusCodeForAFailedToolCall = 502;

        private readonly RequestDelegate _Next;

        public ToolCallFailureStatusCodeMiddleware(RequestDelegate next)
        {
            this._Next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            // Only a message of the protocol is collected. Every other route answers directly, and a request which is
            // no message (a stream which a client opens with GET, for example) would never end, so collecting its
            // answer would hold it back forever.
            if (!HttpMethods.IsPost(context.Request.Method) || !context.Request.Path.StartsWithSegments(CodeUnitSpecificConstants.MCPRoute))
            {
                await this._Next(context);
                return;
            }
            IHttpResponseBodyFeature originalBodyFeature = context.Features.Get<IHttpResponseBodyFeature>();
            using (MemoryStream collectedAnswer = new MemoryStream())
            {
                context.Features.Set<IHttpResponseBodyFeature>(new StreamResponseBodyFeature(collectedAnswer));
                try
                {
                    await this._Next(context);
                }
                finally
                {
                    context.Features.Set(originalBodyFeature);
                }
                if (ToolCallFailure.WasReported(context))
                {
                    context.Response.StatusCode = StatusCodeForAFailedToolCall;
                }
                collectedAnswer.Seek(0, SeekOrigin.Begin);
                await collectedAnswer.CopyToAsync(originalBodyFeature.Stream);
            }
        }
    }
}
