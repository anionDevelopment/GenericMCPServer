using System;

namespace GenericMCPServerBackend.Core.Model
{
    /// <summary>
    /// One access-token of one client.
    ///
    /// A client can have more than one of them at the same time, which is what makes an exchange possible without an
    /// interruption: the new token is created, the client is switched over, and the old one is removed or expires.
    /// </summary>
    public sealed class AccessToken
    {
        public AccessToken(string client, string urlNamespace, string hash, DateTime validUntil)
        {
            this.Client = client;
            this.UrlNamespace = urlNamespace;
            this.Hash = hash;
            this.ValidUntil = validUntil;
        }

        /// <summary>The client this token belongs to. It is what the server knows about the caller.</summary>
        public string Client { get; }

        /// <summary>
        /// The mcp-server this token is valid for. A token belongs to one mcp-server and is valid for all of its
        /// tools: a client is a client of a server and not of a single tool.
        /// </summary>
        public string UrlNamespace { get; }

        /// <summary>
        /// The hash of the token. The token itself is not stored anywhere: whoever gets the file into their hands
        /// therefore holds no access.
        /// </summary>
        public string Hash { get; }

        /// <summary>The point in time (utc) up to which the token is valid. A token without an end does not exist.</summary>
        public DateTime ValidUntil { get; }

        public bool IsValidAt(DateTime pointInTimeInUTC)
        {
            return pointInTimeInUTC < this.ValidUntil;
        }
    }
}
