using System.Text.Json.Serialization;

namespace GenericMCPServerBackend.Core.Model
{
    /// <summary>
    /// What provides the possible values of one argument of a prompt, as it is declared by an administrator in the
    /// tool-definitions-file. This codeunit knows no value of any argument, so every suggestion a client receives comes
    /// from such a declaration.
    ///
    /// A completion is run exactly like a tool (see <see cref="CallableDefinition"/>) and differs in what a client does
    /// with the answer: it offers the lines the script wrote to whoever fills in the argument, instead of reading them
    /// as the result of something which was done. It is declared inside the prompt whose argument it completes, because
    /// it belongs to that one argument and to nothing else.
    /// </summary>
    public class CompletionDefinition : CallableDefinition
    {
        /// <summary>
        /// The name of the parameter with which the script is told which argument it is asked about, in the form
        /// "--argument=&lt;name&gt;".
        ///
        /// It belongs to the mechanism and not to a declaration: a completion completes exactly the one argument it is
        /// declared for, so there is nothing to declare here, and passing it lets one script complete several
        /// arguments.
        /// </summary>
        public const string ArgumentParameterName = "argument";

        /// <summary>
        /// The name of the parameter with which the value the client entered so far is passed to the script, in the
        /// form "--value=&lt;value&gt;". It is empty when nothing was entered yet, which is the usual case for the
        /// first request.
        /// </summary>
        public const string ValueParameterName = "value";

        /// <summary>
        /// The prompt this completion belongs to. It is not declared but taken from the prompt which contains this
        /// declaration, so that a message about this completion names the prompt an administrator finds it in.
        /// </summary>
        [JsonIgnore]
        public string PromptName { get; set; }

        /// <summary>
        /// The argument this completion provides the values of. It is not declared either: it is the key under which
        /// this declaration stands in the prompt, which is what makes a second provider for the same argument
        /// impossible.
        /// </summary>
        [JsonIgnore]
        public string ArgumentName { get; set; }

        /// <inheritdoc/>
        [JsonIgnore]
        public override string Kind => "completion";

        /// <inheritdoc/>
        [JsonIgnore]
        public override string Name => $"{this.PromptName}.{this.ArgumentName}";

        /// <summary>
        /// The environment of a python-completion carries the kind in its name, so that a completion and a tool, a
        /// resource or a prompt of the same name do not end up in the same folder.
        /// </summary>
        [JsonIgnore]
        public override string NameOfTheVirtualEnvironment => $"{this.Kind}.{this.PromptName}.{this.ArgumentName}";
    }
}
