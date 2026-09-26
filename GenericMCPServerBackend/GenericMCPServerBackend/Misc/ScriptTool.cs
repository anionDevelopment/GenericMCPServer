using GenericMCPServerBackend.Core.Model;
using GenericMCPServerBackend.Core.Services;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace GenericMCPServerBackend.Core.Misc
{
    /// <summary>
    /// Makes one declared tool available as a tool of the MCP-server.
    ///
    /// The tools of this server are not known at compile-time, so they can not be methods which carry an attribute:
    /// they are created here at runtime, one instance per declaration. Everything a client sees about a tool (its
    /// name, its description and the schema of its arguments) comes from the declaration.
    /// </summary>
    public class ScriptTool : McpServerTool
    {
        private readonly IBusinessLogicService _BusinessLogicService;

        /// <summary>
        /// Where a call of this tool is remembered while it runs, so that a client which cancels it can stop it.
        /// </summary>
        private readonly RunningToolCalls _RunningToolCalls;

        private readonly ToolDefinition _Definition;

        /// <summary>
        /// The mcp-server this tool belongs to. A tool-name is unambiguous within its mcp-server and not beyond it, so
        /// the namespace is part of what identifies this tool.
        /// </summary>
        private readonly string _UrlNamespace;

        private readonly Tool _ProtocolTool;

        public ScriptTool(IBusinessLogicService businessLogicService, RunningToolCalls runningToolCalls, string urlNamespace, ToolDefinition definition)
        {
            this._BusinessLogicService = businessLogicService;
            this._RunningToolCalls = runningToolCalls;
            this._UrlNamespace = urlNamespace;
            this._Definition = definition;
            this._ProtocolTool = new Tool()
            {
                Name = definition.ToolName,
                Description = definition.Description,
                InputSchema = GetInputSchema(definition),
                // A tool which declared the shape of its answer offers that shape to the client, which is what lets
                // the client read the answer as data instead of as text. A tool which declared none offers none.
                OutputSchema = definition.OutputSchema,
            };
        }

        public override Tool ProtocolTool => this._ProtocolTool;

        /// <summary>
        /// Additional metadata about this tool. A tool which is created from a declaration carries none: everything a
        /// client has to know about it is already part of <see cref="ProtocolTool"/>.
        /// </summary>
        public override IReadOnlyList<object> Metadata => Array.Empty<object>();

        /// <summary>
        /// Builds the json-schema of the arguments of the given tool.
        ///
        /// Every parameter is a required string: a value is passed to the script as text, so a type which is more
        /// specific would only claim a guarantee which nothing here checks. What a value actually has to look like is
        /// checked by the script itself.
        /// </summary>
        private static JsonElement GetInputSchema(ToolDefinition definition)
        {
            Dictionary<string, object> properties = new Dictionary<string, object>();
            foreach (string parameter in definition.Parameters)
            {
                properties[parameter] = new Dictionary<string, string>() { { "type", "string" } };
            }
            return JsonSerializer.SerializeToElement(new Dictionary<string, object>()
            {
                { "type", "object" },
                { "properties", properties },
                { "required", definition.Parameters },
            });
        }

        /// <summary>
        /// Runs the tool and turns its answer into the result the client gets.
        ///
        /// A failure is answered as a result with <see cref="CallToolResult.IsError"/> and the message of the failure,
        /// and not by letting the exception escape: the client (and the model behind it) can only react to a wrong
        /// argument if it learns what was wrong with it. It is additionally reported to
        /// <see cref="ToolCallFailure"/>, which makes the whole answer carry the status-code 502.
        ///
        /// While it runs, the call is one of the running calls (see <see cref="RunningToolCalls"/>), which is what lets
        /// a "notifications/cancelled" of the client stop the program behind it. The token which the SDK hands over here
        /// is not that mechanism and can not be: the transport runs in the stateless mode, so that notification arrives
        /// as an http-request of its own with a session of its own and never reaches the request this call belongs to.
        /// </summary>
        public override ValueTask<CallToolResult> InvokeAsync(RequestContext<CallToolRequestParams> request, CancellationToken cancellationToken = default)
        {
            ToolCallIdentity identity = new ToolCallIdentity(this._UrlNamespace, AccessTokenValidatorMiddleware.GetClientOf(Utilities.GetHttpContextOf(request.Services)), request.JsonRpcRequest.Id);
            using (RunningToolCall runningToolCall = this._RunningToolCalls.Start(identity))
            {
                try
                {
                    Dictionary<string, string> arguments = CallArguments.Read(request.Params?.Arguments);
                    string output = this._BusinessLogicService.RunTool(this._UrlNamespace, this._Definition.ToolName, arguments, runningToolCall.CancellationToken);
                    // An answer which does not fit the declared schema fails the call like every other failure of the
                    // tool: the client was promised a document of that shape, and an answer which is not one is nothing it
                    // could use.
                    JsonElement? structuredContent = null;
                    if (this._Definition.StructuredOutput != null)
                    {
                        structuredContent = this._Definition.StructuredOutput.Read(output);
                    }
                    return new ValueTask<CallToolResult>(CreateResult(output, structuredContent, false));
                }
                catch (OperationCanceledException) when (runningToolCall.WasCancelled)
                {
                    // A call which the client itself cancelled is not reported to ToolCallFailure, so the answer keeps
                    // the status-code of a normal answer: the 502 of a failed call states that the program behind this
                    // server failed, and nothing failed here. The client is told about it all the same - it asked for a
                    // result, it gets no result, and a client which has already stopped listening ignores it anyway.
                    return new ValueTask<CallToolResult>(CreateResult($"The call of the tool \"{this._Definition.ToolName}\" was cancelled by the client.", null, true));
                }
                catch (Exception exception)
                {
                    ToolCallFailure.Report(request.Services);
                    // The message goes to the client, so the values of the credentials are removed from it first: a script
                    // which wrote its credential into a stacktrace would hand it over otherwise.
                    return new ValueTask<CallToolResult>(CreateResult(this._BusinessLogicService.RemoveCredentialValuesFrom(exception.Message), null, true));
                }
            }
        }

        /// <summary>
        /// Builds the result of a call.
        ///
        /// What the tool wrote is always part of it as text, also when it is a json-document which fits a declared
        /// output-schema: a client which reads the structured content receives the data, and a client which does not
        /// receives what it received before. A failure carries no structured content at all - the schema describes
        /// the answer of the tool and not the reason why there is none.
        /// </summary>
        private static CallToolResult CreateResult(string text, JsonElement? structuredContent, bool isError)
        {
            return new CallToolResult()
            {
                Content = new List<ContentBlock>() { new TextContentBlock() { Text = text } },
                StructuredContent = structuredContent,
                IsError = isError,
            };
        }
    }
}
