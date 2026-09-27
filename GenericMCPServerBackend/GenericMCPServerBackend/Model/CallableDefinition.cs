using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace GenericMCPServerBackend.Core.Model
{
    /// <summary>
    /// What an administrator declares for something which the server runs: a tool (<see cref="ToolDefinition"/>), a
    /// resource (<see cref="ResourceDefinition"/>), a prompt (<see cref="PromptDefinition"/>) or what provides the
    /// possible values of an argument of such a prompt (<see cref="CompletionDefinition"/>).
    ///
    /// All of them are run in exactly the same way - a python-script, a shell-script or a web-request - and only what a
    /// client does with the answer differs. Everything which describes that run therefore lives here and not several
    /// times: a change of the mechanism (a further type, another way of handing over a credential) has to apply to
    /// every one of them, and a mechanism which existed more than once could apply to one of them and not to another.
    /// </summary>
    public abstract class CallableDefinition
    {
        /// <summary>
        /// What this declaration is, in the wording of a message to an administrator ("tool", "resource" or "prompt"). A message
        /// about a mistake in a declaration states what was declared, because an administrator looks for it in the
        /// file under exactly that word.
        /// </summary>
        [JsonIgnore]
        public abstract string Kind { get; }

        /// <summary>The declared name, which is what a message about this declaration names it by.</summary>
        [JsonIgnore]
        public abstract string Name { get; }

        /// <summary>
        /// The name of the virtual environment of this declaration, if it is a python-declaration.
        ///
        /// It is stated here and not derived from <see cref="Name"/> by whoever needs it, because the environments of
        /// all declarations of a server lie in one folder: a resource and a tool which carry the same name would
        /// otherwise share one environment and rebuild it for each other whenever their requirements differ.
        /// </summary>
        [JsonIgnore]
        public abstract string NameOfTheVirtualEnvironment { get; }

        /// <summary>
        /// The kind of script which implements this declaration. The allowed values are the ones of
        /// <see cref="ToolTypes"/>. The type has to be declared.
        /// </summary>
        [JsonPropertyName("type")]
        public string Type { get; set; }

        /// <summary>
        /// The script which implements this declaration. A relative path is resolved against the folder of the
        /// tool-definitions-file, so the declaration and the scripts it points to belong together.
        /// </summary>
        [JsonPropertyName("calls")]
        public string Calls { get; set; }

        /// <summary>
        /// The packages the script of a python-declaration needs, in the notation of a requirements.txt (for example
        /// "requests==2.32.3"). They are installed into a virtual environment which belongs to this declaration alone,
        /// so two of them can need two versions of the same package without disturbing each other.
        ///
        /// Only a python-declaration can have requirements: for another type they would claim an effect which nothing
        /// has.
        /// </summary>
        [JsonPropertyName("requirements")]
        public IList<string> Requirements { get; set; } = new List<string>();

        /// <summary>
        /// What this declaration offers. This text is what a client (and the model behind it) reads to decide whether
        /// it is the right one for a task, so it should describe the answer and not the implementation.
        /// </summary>
        [JsonPropertyName("description")]
        public string Description { get; set; }

        /// <summary>
        /// The names of the parameters. Each of them is passed to the script as "--&lt;name&gt;=&lt;value&gt;", so a
        /// name here has to be the name of an option the script accepts.
        ///
        /// A tool of the type <see cref="ToolType.WebRequest"/>, a resource and a completion declare no parameters:
        /// each of them takes exactly the parameters which its mechanism defines. A prompt declares the arguments
        /// which are filled into its text.
        /// </summary>
        [JsonPropertyName("parameters")]
        public IList<string> Parameters { get; set; } = new List<string>();

        /// <summary>
        /// The credentials which are needed. The key is the name they are seen under (the environment-variable of the
        /// script, or the name a header refers to), the value is the name of the environment-variable of the server it
        /// is read from.
        ///
        /// The two names are stated separately so that the same script can be declared twice and use a different
        /// access each time. Exactly the credentials which are stated here are handed over and no other.
        /// </summary>
        [JsonPropertyName("credentials")]
        public IDictionary<string, string> Credentials { get; set; } = new Dictionary<string, string>();

        /// <summary>
        /// The headers which a declaration of the type <see cref="ToolType.WebRequest"/> sends. A value may refer to a
        /// credential of its own declaration with "${&lt;name&gt;}" (for example "Bearer ${TOKEN}"); a value without
        /// such a reference is a fixed header.
        /// </summary>
        [JsonPropertyName("headers")]
        public IDictionary<string, string> Headers { get; set; } = new Dictionary<string, string>();

        /// <summary>
        /// The shape of the answer, as a json-schema. A tool which states one answers with a json-document which fits
        /// it, and that document reaches the client as the structured content of the result next to the text of the
        /// answer.
        ///
        /// Only a tool has one: a resource states the <see cref="ResourceDefinition.MimeType"/> of its content, a
        /// prompt writes the text of the prompt and a completion writes one value per line, so none of them answers
        /// with something whose shape a client reads as data. A tool which states none answers with text, exactly as
        /// it did before.
        /// </summary>
        [JsonPropertyName("output-schema")]
        public JsonElement? OutputSchema { get; set; }

        /// <summary>
        /// The addresses a tool of the type <see cref="ToolType.WebRequest"/> may request, as regular expressions.
        ///
        /// An address contains no protocol here, because the protocol is always https, and a rule has to match the
        /// whole address ("example\.com/.*" therefore allows everything below example.com, while "example\.com" allows
        /// exactly that one address). Everything which no rule matches is denied, so a tool without a rule can reach
        /// nothing.
        /// </summary>
        [JsonPropertyName("allowed-urls")]
        public IList<string> AllowedUrls { get; set; } = new List<string>();
    }
}
