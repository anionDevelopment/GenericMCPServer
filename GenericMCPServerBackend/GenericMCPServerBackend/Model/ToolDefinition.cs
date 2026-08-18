using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace GenericMCPServerBackend.Core.Model
{
    /// <summary>
    /// One tool as it is declared by an administrator in the tool-definitions-file. This codeunit contains no tool of
    /// its own, so every tool the server offers comes from such a declaration.
    /// </summary>
    public class ToolDefinition
    {
        /// <summary>
        /// The name under which the tool is offered. This is the name a client uses to call it.
        /// </summary>
        [JsonPropertyName("tool-name")]
        public string ToolName { get; set; }

        /// <summary>
        /// The script which implements the tool. A relative path is resolved against the folder of the
        /// tool-definitions-file, so the declaration and the scripts it points to belong together.
        /// </summary>
        [JsonPropertyName("calls")]
        public string Calls { get; set; }

        /// <summary>
        /// What the tool does. This text is what a client (and the model behind it) reads to decide whether the tool
        /// is the right one for a task, so it should describe the answer and not the implementation.
        /// </summary>
        [JsonPropertyName("description")]
        public string Description { get; set; }

        /// <summary>
        /// The names of the parameters of the tool. Each of them is passed to the script as "--&lt;name&gt;=&lt;value&gt;", so
        /// a name here has to be the name of an option the script accepts.
        /// </summary>
        [JsonPropertyName("parameters")]
        public IList<string> Parameters { get; set; } = new List<string>();
    }
}
