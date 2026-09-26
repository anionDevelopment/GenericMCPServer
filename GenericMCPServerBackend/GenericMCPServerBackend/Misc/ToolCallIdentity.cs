using ModelContextProtocol.Protocol;
using System;

namespace GenericMCPServerBackend.Core.Misc
{
    /// <summary>
    /// Which tool-call a message of the protocol means.
    ///
    /// A client which cancels a tool-call names it by the id of the request it sent that call with, and such an id is
    /// only unambiguous within the client which chose it: two clients may both call their first request "1". The
    /// mcp-server and the calling client are therefore part of what identifies a call, so that a cancellation can only
    /// stop a call of the client which sent it and only one of the mcp-server it was sent to.
    ///
    /// An mcp-server which is declared as anonymous has no calling client here, because it has no clients at all:
    /// everybody who reaches it is the same caller, which is exactly what declaring it as anonymous means.
    /// </summary>
    public sealed class ToolCallIdentity : IEquatable<ToolCallIdentity>
    {
        /// <summary>The mcp-server the call belongs to.</summary>
        private readonly string _UrlNamespace;

        /// <summary>The client the call belongs to, or null when the mcp-server is one which everybody may use.</summary>
        private readonly string _Client;

        /// <summary>The id of the request the call was sent with, which is what a cancellation names it by.</summary>
        private readonly RequestId _RequestId;

        public ToolCallIdentity(string urlNamespace, string client, RequestId requestId)
        {
            this._UrlNamespace = urlNamespace;
            this._Client = client;
            this._RequestId = requestId;
        }

        public bool Equals(ToolCallIdentity other)
        {
            return other != null
                && string.Equals(this._UrlNamespace, other._UrlNamespace, StringComparison.Ordinal)
                && string.Equals(this._Client, other._Client, StringComparison.Ordinal)
                && this._RequestId.Equals(other._RequestId);
        }

        public override bool Equals(object obj)
        {
            return this.Equals(obj as ToolCallIdentity);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(this._UrlNamespace, this._Client, this._RequestId);
        }

        /// <summary>
        /// States which call this is, for a message which a reader of the log has to be able to assign to a caller.
        /// </summary>
        public override string ToString()
        {
            return $"the request {this._RequestId} of {(this._Client == null ? "an anonymous caller" : $"the client \"{this._Client}\"")} at the mcp-server \"{this._UrlNamespace}\"";
        }
    }
}
