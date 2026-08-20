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
        /// Whether the tool can be used without an access-token. Without this, a tool is only available to a client
        /// which authenticated itself.
        ///
        /// It is stated per tool and not per server, so that a server can offer a tool which everybody may use next to
        /// tools which only a known client may use.
        /// </summary>
        [JsonPropertyName("anonymous")]
        public bool Anonymous { get; set; }

        /// <summary>
        /// The kind of script which implements the tool. The allowed values are the ones of <see cref="ToolTypes"/>.
        /// The type has to be declared for every tool.
        /// </summary>
        [JsonPropertyName("type")]
        public string Type { get; set; }

        /// <summary>
        /// The script which implements the tool. A relative path is resolved against the folder of the
        /// tool-definitions-file, so the declaration and the scripts it points to belong together.
        /// </summary>
        [JsonPropertyName("calls")]
        public string Calls { get; set; }

        /// <summary>
        /// The packages the script of a python-tool needs, in the notation of a requirements.txt (for example
        /// "requests==2.32.3"). They are installed into a virtual environment which belongs to this tool alone, so two
        /// tools can need two versions of the same package without disturbing each other.
        ///
        /// Only a python-tool can have requirements: for another type they would claim an effect which nothing has.
        /// </summary>
        [JsonPropertyName("requirements")]
        public IList<string> Requirements { get; set; } = new List<string>();

        /// <summary>
        /// What the tool does. This text is what a client (and the model behind it) reads to decide whether the tool
        /// is the right one for a task, so it should describe the answer and not the implementation.
        /// </summary>
        [JsonPropertyName("description")]
        public string Description { get; set; }

        /// <summary>
        /// The names of the parameters of the tool. Each of them is passed to the script as "--&lt;name&gt;=&lt;value&gt;", so
        /// a name here has to be the name of an option the script accepts.
        ///
        /// A tool of the type <see cref="ToolType.WebRequest"/> has no declared parameters: it takes exactly the one
        /// parameter which the mechanism defines, namely the address which is requested.
        /// </summary>
        [JsonPropertyName("parameters")]
        public IList<string> Parameters { get; set; } = new List<string>();

        /// <summary>
        /// The credentials the tool needs. The key is the name the tool sees it under (the environment-variable of the
        /// script, or the name a header refers to), the value is the name of the environment-variable of the server it
        /// is read from.
        ///
        /// The two names are stated separately so that the same script can be declared twice and use a different
        /// access each time. A tool receives exactly the credentials which are stated here and no other.
        /// </summary>
        [JsonPropertyName("credentials")]
        public IDictionary<string, string> Credentials { get; set; } = new Dictionary<string, string>();

        /// <summary>
        /// The headers a tool of the type <see cref="ToolType.WebRequest"/> sends. A value may refer to a credential of
        /// this tool with "${&lt;name&gt;}" (for example "Bearer ${TOKEN}"); a value without such a reference is a fixed
        /// header.
        /// </summary>
        [JsonPropertyName("headers")]
        public IDictionary<string, string> Headers { get; set; } = new Dictionary<string, string>();

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
