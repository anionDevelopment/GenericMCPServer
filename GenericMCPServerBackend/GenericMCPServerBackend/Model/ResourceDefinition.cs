using System.Text.Json.Serialization;

namespace GenericMCPServerBackend.Core.Model
{
    /// <summary>
    /// One resource as it is declared by an administrator in the tool-definitions-file. This codeunit contains no
    /// resource of its own, so every resource the server offers comes from such a declaration.
    ///
    /// A resource is content which a client reads by its uri, while a tool is something which a client calls with
    /// values it chooses. That is the whole difference here: a resource is run exactly like a tool (see
    /// <see cref="CallableDefinition"/>), but nothing of what a client sends decides what it answers - which is why a
    /// resource takes no parameter and why its declaration states the uri under which it is read.
    /// </summary>
    public class ResourceDefinition : CallableDefinition
    {
        /// <summary>
        /// The name of the parameter with which the uri of the resource is passed to its script, in the form
        /// "--uri=&lt;uri&gt;".
        ///
        /// It belongs to the mechanism and not to a declaration: a resource has exactly one uri, so there is nothing
        /// to declare here, and passing it lets one script implement several resources.
        /// </summary>
        public const string UriParameterName = "uri";

        /// <summary>
        /// The uri under which the resource is read. It is what a client sees in the list of the resources and what it
        /// sends to read the content, so it identifies the resource within its mcp-server.
        ///
        /// For a resource of the type <see cref="ToolType.WebRequest"/> it is additionally the address which is
        /// requested, so such a resource states an https-address here.
        /// </summary>
        [JsonPropertyName("uri")]
        public string Uri { get; set; }

        /// <summary>
        /// The name under which the resource is offered. It is what a client shows a reader; the uri is what it reads
        /// the content by.
        /// </summary>
        [JsonPropertyName("resource-name")]
        public string ResourceName { get; set; }

        /// <summary>
        /// The type of the content, for example "text/markdown". A client needs it to know what it received, so it is
        /// declared and not guessed from the content or from the uri.
        /// </summary>
        [JsonPropertyName("mime-type")]
        public string MimeType { get; set; }

        /// <inheritdoc/>
        [JsonIgnore]
        public override string Kind => "resource";

        /// <inheritdoc/>
        [JsonIgnore]
        public override string Name => this.ResourceName;

        /// <summary>
        /// The environment of a python-resource carries the kind in its name, so that a resource and a tool of the
        /// same name do not end up in the same folder.
        /// </summary>
        [JsonIgnore]
        public override string NameOfTheVirtualEnvironment => $"{this.Kind}.{this.ResourceName}";
    }
}
