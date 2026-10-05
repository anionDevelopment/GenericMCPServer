using GenericMCPServerBackend.Core.Services;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace GenericMCPServerBackend.Core.Misc
{
    /// <summary>
    /// Answers the request with which a client asks which values an argument of a prompt can have.
    ///
    /// Unlike a tool, a resource and a prompt, a completion is no primitive of the SDK which could be put into a
    /// collection: the protocol has one request for all of them, so this is a handler of that request and not one
    /// object per declaration. One instance belongs to one mcp-server, exactly like one collection of tools does, so
    /// the completions of one mcp-server can neither be seen nor reached by a client of another one.
    /// </summary>
    public class ScriptCompletionHandler
    {
        /// <summary>
        /// The amount of values which one answer carries. The protocol states this limit, and an answer which exceeds
        /// it would be one a client is allowed to reject; how many values there are is handed over with the answer, so
        /// that whoever asked learns that the list is not complete.
        /// </summary>
        private const int MaximalAmountOfValues = 100;

        private readonly IBusinessLogicService _BusinessLogicService;

        /// <summary>
        /// The mcp-server whose completions this handler answers. A prompt-name is unambiguous within its mcp-server
        /// and not beyond it, so the namespace is part of what identifies the prompt a request asks about.
        /// </summary>
        private readonly string _UrlNamespace;

        public ScriptCompletionHandler(IBusinessLogicService businessLogicService, string urlNamespace)
        {
            this._BusinessLogicService = businessLogicService;
            this._UrlNamespace = urlNamespace;
        }

        /// <summary>
        /// Runs the completion which is declared for the argument the request names and turns the values it wrote into
        /// the result the client gets.
        ///
        /// A failure is answered as an error of the protocol, exactly like a failed read of a resource and a prompt
        /// which could not be built: a client which asked which values are possible must not receive the reason of a
        /// failure as if it were one of them. The values of the credentials are removed from the message beforehand,
        /// because a script which wrote its credential into a stacktrace would hand it over otherwise.
        /// </summary>
        public ValueTask<CompleteResult> HandleAsync(RequestContext<CompleteRequestParams> request, CancellationToken cancellationToken = default)
        {
            IReadOnlyList<string> values;
            try
            {
                values = this.GetValues(request.Params);
            }
            catch (Exception exception)
            {
                throw new McpException(this._BusinessLogicService.RemoveCredentialValuesFrom(exception.Message), exception);
            }
            return new ValueTask<CompleteResult>(new CompleteResult() { Completion = CreateCompletion(values) });
        }

        /// <summary>
        /// Builds what is handed over for the given values.
        ///
        /// Only the first <see cref="MaximalAmountOfValues"/> of them are carried by the answer, and how many there are
        /// in total is stated next to them: a client which asked for the possible values has to be able to tell "these
        /// are all of them" from "these are the first of them", and it can not do that by counting.
        /// </summary>
        public static Completion CreateCompletion(IReadOnlyList<string> values)
        {
            return new Completion()
            {
                Values = values.Take(MaximalAmountOfValues).ToList(),
                Total = values.Count,
                HasMore = MaximalAmountOfValues < values.Count,
            };
        }

        /// <summary>
        /// The values which the request asks for.
        ///
        /// Only a reference to a prompt is answered with values: the other reference the protocol knows is a
        /// resource-template, and this server declares every resource with the uri it has and derives none from a
        /// template, so such a reference names nothing which could be completed here. A request which names neither a
        /// prompt nor an argument is answered in the same way - with no value, which is the answer to "which values are
        /// possible" and not a failure.
        /// </summary>
        private IReadOnlyList<string> GetValues(CompleteRequestParams parameters)
        {
            if (parameters?.Ref is not PromptReference prompt || parameters.Argument == null)
            {
                return new List<string>();
            }
            return this._BusinessLogicService.GetCompletions(this._UrlNamespace, prompt.Name, parameters.Argument.Name, parameters.Argument.Value);
        }
    }
}
