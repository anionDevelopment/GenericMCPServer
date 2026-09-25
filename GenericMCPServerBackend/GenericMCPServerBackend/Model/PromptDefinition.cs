using System.Text.Json.Serialization;

namespace GenericMCPServerBackend.Core.Model
{
    /// <summary>
    /// One prompt as it is declared by an administrator in the tool-definitions-file. This codeunit contains no prompt
    /// of its own, so every prompt the server offers comes from such a declaration.
    ///
    /// A prompt is a text which a client asks for by its name and which it hands to the model behind it, while a tool
    /// is something which a client calls to have it done and a resource is content which it reads by a uri. That is
    /// the whole difference here: a prompt is run exactly like a tool (see <see cref="CallableDefinition"/>), and what
    /// the script writes is the text of the prompt instead of the answer of a call.
    /// </summary>
    public class PromptDefinition : CallableDefinition
    {
        /// <summary>
        /// The name under which the prompt is offered. This is the name a client uses to ask for it.
        /// </summary>
        [JsonPropertyName("prompt-name")]
        public string PromptName { get; set; }

        /// <inheritdoc/>
        [JsonIgnore]
        public override string Kind => "prompt";

        /// <inheritdoc/>
        [JsonIgnore]
        public override string Name => this.PromptName;

        /// <summary>
        /// The environment of a python-prompt carries the kind in its name, so that a prompt and a tool of the same
        /// name do not end up in the same folder.
        /// </summary>
        [JsonIgnore]
        public override string NameOfTheVirtualEnvironment => $"{this.Kind}.{this.PromptName}";
    }
}
