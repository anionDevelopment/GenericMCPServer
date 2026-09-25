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
        /// The whole address is "&lt;domain&gt;/&lt;url-namespace&gt;".
        /// </summary>
        [JsonPropertyName("url-namespace")]
        public string UrlNamespace { get; set; }

        /// <summary>
        /// Whether this server can be used without an access-token. Without this, only a client with a token which was
        /// created for this server reaches it.
        /// </summary>
        [JsonPropertyName("anonymous")]
        public bool Anonymous { get; set; }

        /// <summary>
        /// The roles which may use this mcp-server. A client reaches it when it is in at least one of them; which
        /// client is in which role is stated in the roles-file of the server.
        ///
        /// Single clients or tokens are deliberately not stated here: a declaration states what a role may do, and who
        /// is in that role is decided by whoever administrates the clients - two decisions which are usually made by
        /// two different people.
        /// </summary>
        [JsonPropertyName("authorized-roles")]
        public IList<string> AuthorizedRoles { get; set; } = new List<string>();

        /// <summary>The tools this server offers. A tool belongs to exactly one server.</summary>
        [JsonPropertyName("tools")]
        public IList<ToolDefinition> Tools { get; set; } = new List<ToolDefinition>();

        /// <summary>
        /// The resources this server offers. A resource belongs to exactly one server, exactly like a tool: a client
        /// of one server can neither see nor read the resources of another one.
        /// </summary>
        [JsonPropertyName("resources")]
        public IList<ResourceDefinition> Resources { get; set; } = new List<ResourceDefinition>();

        /// <summary>
        /// The prompts this server offers. A prompt belongs to exactly one server, exactly like a tool and a resource:
        /// a client of one server can neither see nor ask for the prompts of another one.
        /// </summary>
        [JsonPropertyName("prompts")]
        public IList<PromptDefinition> Prompts { get; set; } = new List<PromptDefinition>();
    }
}
