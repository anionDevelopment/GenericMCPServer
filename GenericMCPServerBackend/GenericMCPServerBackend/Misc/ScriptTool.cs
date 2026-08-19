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
        private readonly ToolDefinition _Definition;
        private readonly Tool _ProtocolTool;

        public ScriptTool(IBusinessLogicService businessLogicService, ToolDefinition definition)
        {
            this._BusinessLogicService = businessLogicService;
            this._Definition = definition;
            this._ProtocolTool = new Tool()
            {
                Name = definition.ToolName,
                Description = definition.Description,
                InputSchema = GetInputSchema(definition),
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
        /// argument if it learns what was wrong with it.
        /// </summary>
        public override ValueTask<CallToolResult> InvokeAsync(RequestContext<CallToolRequestParams> request, CancellationToken cancellationToken = default)
        {
            try
            {
                Dictionary<string, string> arguments = new Dictionary<string, string>();
                if (request.Params?.Arguments != null)
                {
                    foreach (KeyValuePair<string, JsonElement> argument in request.Params.Arguments)
                    {
                        // A value which is not a json-string is passed on in its json-form, so that a client which
                        // sends a number does not get an error about a missing argument.
                        arguments[argument.Key] = argument.Value.ValueKind == JsonValueKind.String ? argument.Value.GetString() : argument.Value.GetRawText();
                    }
                }
                string output = this._BusinessLogicService.RunTool(this._Definition.ToolName, arguments);
                return new ValueTask<CallToolResult>(CreateResult(output, false));
            }
            catch (Exception exception)
            {
                return new ValueTask<CallToolResult>(CreateResult(exception.Message, true));
            }
        }

        private static CallToolResult CreateResult(string text, bool isError)
        {
            return new CallToolResult()
            {
                Content = new List<ContentBlock>() { new TextContentBlock() { Text = text } },
                IsError = isError,
            };
        }
    }
}
