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
    /// Makes one declared prompt available as a prompt of the MCP-server.
    ///
    /// Like the tools and the resources, the prompts of this server are not known at compile-time, so they can not be
    /// methods which carry an attribute: they are created here at runtime, one instance per declaration. Everything a
    /// client sees about a prompt (its name, its description and the arguments it takes) comes from the declaration.
    /// </summary>
    public class ScriptPrompt : McpServerPrompt
    {
        private readonly IBusinessLogicService _BusinessLogicService;
        private readonly PromptDefinition _Definition;

        /// <summary>
        /// The mcp-server this prompt belongs to. A prompt-name is unambiguous within its mcp-server and not beyond it,
        /// so the namespace is part of what identifies this prompt.
        /// </summary>
        private readonly string _UrlNamespace;

        private readonly Prompt _ProtocolPrompt;

        public ScriptPrompt(IBusinessLogicService businessLogicService, string urlNamespace, PromptDefinition definition)
        {
            this._BusinessLogicService = businessLogicService;
            this._UrlNamespace = urlNamespace;
            this._Definition = definition;
            this._ProtocolPrompt = new Prompt()
            {
                Name = definition.PromptName,
                Description = definition.Description,
                Arguments = GetArguments(definition),
            };
        }

        public override Prompt ProtocolPrompt => this._ProtocolPrompt;

        /// <summary>
        /// Additional metadata about this prompt. A prompt which is created from a declaration carries none: everything
        /// a client has to know about it is already part of <see cref="ProtocolPrompt"/>.
        /// </summary>
        public override IReadOnlyList<object> Metadata => Array.Empty<object>();

        /// <summary>
        /// Builds the arguments of the given prompt.
        ///
        /// Every declared argument is a required one, exactly like the parameter of a tool: a value is handed to the
        /// script as text, and an argument which the script needs but did not receive is a failure of that script and
        /// not something this server could answer in its place.
        /// </summary>
        private static IList<PromptArgument> GetArguments(PromptDefinition definition)
        {
            List<PromptArgument> result = new List<PromptArgument>();
            foreach (string parameter in definition.Parameters)
            {
                result.Add(new PromptArgument() { Name = parameter, Required = true });
            }
            return result;
        }

        /// <summary>
        /// Runs the prompt and turns the text it wrote into the result the client gets.
        ///
        /// The text becomes one message of the role <see cref="Role.User"/>: a prompt of this server is a text which a
        /// client hands to its model, and a declaration states that text and not a conversation of several turns.
        ///
        /// A failure is answered as an error of the protocol and not as a message, because a client which asked for a
        /// prompt must not hand the reason of a failure to its model as if it were that prompt. The reason is handed
        /// over with it as a <see cref="McpException"/>, whose message is the one which reaches the remote endpoint; the
        /// values of the credentials are removed from it beforehand, because a script which wrote its credential into a
        /// stacktrace would hand it over otherwise.
        /// </summary>
        public override ValueTask<GetPromptResult> GetAsync(RequestContext<GetPromptRequestParams> request, CancellationToken cancellationToken = default)
        {
            string text;
            try
            {
                text = this._BusinessLogicService.GetPrompt(this._UrlNamespace, this._Definition.PromptName, CallArguments.Read(request.Params?.Arguments));
            }
            catch (Exception exception)
            {
                throw new McpException(this._BusinessLogicService.RemoveCredentialValuesFrom(exception.Message), exception);
            }
            return new ValueTask<GetPromptResult>(new GetPromptResult()
            {
                Description = this._Definition.Description,
                Messages = new List<PromptMessage>()
                {
                    new PromptMessage()
                    {
                        Role = Role.User,
                        Content = new TextContentBlock() { Text = text },
                    },
                },
            });
        }
    }
}
