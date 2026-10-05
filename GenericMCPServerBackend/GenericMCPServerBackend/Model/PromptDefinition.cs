using System.Collections.Generic;
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
    ///
    /// A prompt is also the only declaration whose arguments can state which values they may have (see
    /// <see cref="Completions"/>), because a client fills in those arguments itself.
    /// </summary>
    public class PromptDefinition : CallableDefinition
    {
        /// <summary>
        /// The name under which the prompt is offered. This is the name a client uses to ask for it.
        /// </summary>
        [JsonPropertyName("prompt-name")]
        public string PromptName { get; set; }

        /// <summary>
        /// What provides the possible values of the arguments of this prompt, keyed by the name of the argument. An
        /// argument without an entry here is one whose values a client states without being offered any.
        ///
        /// It is a map and not a list, because an argument has one provider or none: two providers for the same
        /// argument would make it undecidable which of them is asked.
        /// </summary>
        [JsonPropertyName("completions")]
        public IDictionary<string, CompletionDefinition> Completions { get; set; } = new Dictionary<string, CompletionDefinition>();

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
