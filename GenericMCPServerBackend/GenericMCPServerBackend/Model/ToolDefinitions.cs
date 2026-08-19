using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace GenericMCPServerBackend.Core.Model
{
    /// <summary>
    /// The content of the tool-definitions-file: the complete list of the tools the server offers.
    /// </summary>
    public class ToolDefinitions
    {
        [JsonPropertyName("tools")]
        public IList<ToolDefinition> Tools { get; set; } = new List<ToolDefinition>();
    }
}
