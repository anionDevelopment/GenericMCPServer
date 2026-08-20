using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace GenericMCPServerBackend.Core.Model
{
    /// <summary>
    /// One mcp-server which this process hosts.
    ///
    /// It is what a client is configured for: its namespace decides the address under which it is reachable, and its
    /// tools are the only ones which a client of it can see. Whether it needs an access-token is decided here as well
    /// and not per tool, because a client belongs to a server and not to a single tool of it.
    /// </summary>
    public class MCPServerDefinition
    {
        /// <summary>
        /// The part of the address which this server is reachable under, for example "teamA" or "teamA/mcpserver1".
        /// The whole address is "&lt;domain&gt;/&lt;url-namespace&gt;/message".
        /// </summary>
        [JsonPropertyName("url-namespace")]
        public string UrlNamespace { get; set; }

        /// <summary>
        /// Whether this server can be used without an access-token. Without this, only a client with a token which was
        /// created for this server reaches it.
        /// </summary>
        [JsonPropertyName("anonymous")]
        public bool Anonymous { get; set; }

        /// <summary>The tools this server offers. A tool belongs to exactly one server.</summary>
        [JsonPropertyName("tools")]
        public IList<ToolDefinition> Tools { get; set; } = new List<ToolDefinition>();
    }
}
