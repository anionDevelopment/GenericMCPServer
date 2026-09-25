using GenericMCPServerBackend.Core.Model;
using GenericMCPServerBackend.Core.Services;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace GenericMCPServerBackend.Core.Misc
{
    /// <summary>
    /// Makes one declared resource available as a resource of the MCP-server.
    ///
    /// Like the tools, the resources of this server are not known at compile-time, so they can not be methods which
    /// carry an attribute: they are created here at runtime, one instance per declaration. Everything a client sees
    /// about a resource (its uri, its name, its description and the type of its content) comes from the declaration.
    /// </summary>
    public class ScriptResource : McpServerResource
    {
        private readonly IBusinessLogicService _BusinessLogicService;
        private readonly ResourceDefinition _Definition;

        /// <summary>
        /// The mcp-server this resource belongs to. A uri is unambiguous within its mcp-server and not beyond it, so
        /// the namespace is part of what identifies this resource.
        /// </summary>
        private readonly string _UrlNamespace;

        private readonly ResourceTemplate _ProtocolResourceTemplate;

        public ScriptResource(IBusinessLogicService businessLogicService, string urlNamespace, ResourceDefinition definition)
        {
            this._BusinessLogicService = businessLogicService;
            this._UrlNamespace = urlNamespace;
            this._Definition = definition;
            this._ProtocolResourceTemplate = new ResourceTemplate()
            {
                UriTemplate = definition.Uri,
                Name = definition.ResourceName,
                Description = definition.Description,
                MimeType = definition.MimeType,
            };
        }

        public override ResourceTemplate ProtocolResourceTemplate => this._ProtocolResourceTemplate;

        /// <summary>
        /// Additional metadata about this resource. A resource which is created from a declaration carries none:
        /// everything a client has to know about it is already part of <see cref="ProtocolResourceTemplate"/>.
        /// </summary>
        public override IReadOnlyList<object> Metadata => Array.Empty<object>();

        /// <summary>
        /// Whether the given uri is the uri of this resource.
        ///
        /// A resource is declared with the uri it has, so it is exactly that uri which reads it - a declaration
        /// contains no template which a client could fill in. The comparison respects the case, because a uri does.
        /// </summary>
        public override bool IsMatch(string uri)
        {
            return string.Equals(uri, this._Definition.Uri, StringComparison.Ordinal);
        }

        /// <summary>
        /// Reads the resource and turns its content into the result the client gets.
        ///
        /// A failure is answered as an error of the protocol and not as a content, because a client which asked for a
        /// document must not receive the reason of a failure as if it were that document. The reason is handed over
        /// with it as a <see cref="McpException"/>, whose message is the one which reaches the remote endpoint; the
        /// values of the credentials are removed from it beforehand, because a script which wrote its credential into
        /// a stacktrace would hand it over otherwise.
        /// </summary>
        public override ValueTask<ReadResourceResult> ReadAsync(RequestContext<ReadResourceRequestParams> request, CancellationToken cancellationToken = default)
        {
            string content;
            try
            {
                content = this._BusinessLogicService.ReadResource(this._UrlNamespace, this._Definition.Uri);
            }
            catch (Exception exception)
            {
                throw new McpException(this._BusinessLogicService.RemoveCredentialValuesFrom(exception.Message), exception);
            }
            return new ValueTask<ReadResourceResult>(new ReadResourceResult()
            {
                Contents = new List<ResourceContents>()
                {
                    new TextResourceContents()
                    {
                        Uri = this._Definition.Uri,
                        MimeType = this._Definition.MimeType,
                        Text = content,
                    },
                },
            });
        }
    }
}
