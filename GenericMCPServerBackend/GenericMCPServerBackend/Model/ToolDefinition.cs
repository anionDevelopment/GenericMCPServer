using System.Text.Json.Serialization;

namespace GenericMCPServerBackend.Core.Model
{
    /// <summary>
    /// One tool as it is declared by an administrator in the tool-definitions-file. This codeunit contains no tool of
    /// its own, so every tool the server offers comes from such a declaration.
    ///
    /// A tool is called by a client with the values of its parameters; everything about how it is run is stated in
    /// <see cref="CallableDefinition"/>, which a resource states in the same way.
    /// </summary>
    public class ToolDefinition : CallableDefinition
    {
        /// <summary>
        /// The name under which the tool is offered. This is the name a client uses to call it.
        /// </summary>
        [JsonPropertyName("tool-name")]
        public string ToolName { get; set; }

        /// <inheritdoc/>
        [JsonIgnore]
        public override string Kind => "tool";

        /// <inheritdoc/>
        [JsonIgnore]
        public override string Name => this.ToolName;

        /// <summary>
        /// The environment of a python-tool is named after the tool, which is the name an administrator finds the
        /// folder of that environment under.
        /// </summary>
        [JsonIgnore]
        public override string NameOfTheVirtualEnvironment => this.ToolName;
    }
}
