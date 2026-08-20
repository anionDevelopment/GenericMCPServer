using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace GenericMCPServerBackend.Core.Model
{
    /// <summary>
    /// The content of the tool-definitions-file: the mcp-servers this process hosts.
    ///
    /// One process can host more than one mcp-server, each of them with its own tools and its own clients. They are
    /// separated by a namespace in the url, so a client which is configured for one of them can neither see nor reach
    /// the tools of another one.
    /// </summary>
    public class ToolDefinitions
    {
        [JsonPropertyName("mcp-servers")]
        public IList<MCPServerDefinition> MCPServers { get; set; } = new List<MCPServerDefinition>();

        /// <summary>
        /// The tools as they were declared before the mcp-servers existed. It is only read to be able to tell an
        /// administrator that the file has to be written in the current form; the tools of such a file are not used.
        /// </summary>
        [JsonPropertyName("tools")]
        public IList<ToolDefinition> ToolsOfTheFormerForm { get; set; } = new List<ToolDefinition>();
    }
}
