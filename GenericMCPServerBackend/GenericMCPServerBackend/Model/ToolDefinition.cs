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

        /// <summary>
        /// What checks the answers of this tool against the declared <see cref="CallableDefinition.OutputSchema"/>,
        /// or null when this tool declared none and therefore answers with text alone.
        ///
        /// It is not declared but built from the declared schema while the declarations are read (see
        /// <see cref="Services.ToolDefinitionsReader"/>), exactly like the parameters which a mechanism defines: a
        /// schema which is not usable is a mistake of the configuration and has to be visible when the server starts.
        /// </summary>
        [JsonIgnore]
        public StructuredOutput StructuredOutput { get; set; }

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
