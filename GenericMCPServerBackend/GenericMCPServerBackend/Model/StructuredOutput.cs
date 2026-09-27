using NJsonSchema;
using NJsonSchema.Validation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace GenericMCPServerBackend.Core.Model
{
    /// <summary>
    /// The output-schema which a tool declared, in the form in which it can check an answer.
    ///
    /// It is built once, while the declarations are read, and not for every call: a schema which is not usable is a
    /// mistake of the configuration and has to be visible when the server starts, and building it again for every call
    /// would do the same work over and over.
    ///
    /// What a tool answers is text, because that is what a script writes. A tool which declared a schema states with
    /// it that this text is a json-document of a known shape, so whether it really is one is checked here: a client
    /// which was promised data must not receive something else under that promise.
    /// </summary>
    public sealed class StructuredOutput
    {
        private readonly JsonSchema _Schema;

        /// <summary>The tool this schema belongs to, so that a message about an answer names the tool it came from.</summary>
        private readonly string _NameOfTheTool;

        private StructuredOutput(JsonSchema schema, string nameOfTheTool)
        {
            this._Schema = schema;
            this._NameOfTheTool = nameOfTheTool;
        }

        /// <summary>
        /// Builds what checks the answers of the given tool from the schema it declared.
        /// </summary>
        /// <param name="declaration">The declared schema, which has to be a json-object.</param>
        /// <param name="nameOfTheTool">The name of the tool the schema belongs to.</param>
        /// <exception cref="InvalidOperationException">Thrown when the declared schema is not a usable json-schema. The message states why it can not be read; in which declaration that happened is stated by whoever read that declaration.</exception>
        public static StructuredOutput Create(JsonElement declaration, string nameOfTheTool)
        {
            JsonSchema schema;
            try
            {
                schema = JsonSchema.FromJsonAsync(declaration.GetRawText()).GetAwaiter().GetResult();
            }
            catch (Exception exception)
            {
                throw new InvalidOperationException($"The declared json-schema can not be read: {exception.Message}", exception);
            }
            return new StructuredOutput(schema, nameOfTheTool);
        }

        /// <summary>
        /// Reads what the tool answered as the structured content of the result.
        /// </summary>
        /// <param name="output">What the tool wrote.</param>
        /// <returns>The answer as the json-document it states to be.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the answer is no json-document or does not fit the declared schema. The tool-call fails with that message, exactly like a script which ended with an exit-code which is not 0: an answer which does not fit the schema is an answer the client can not use.</exception>
        public JsonElement Read(string output)
        {
            JsonDocument document;
            try
            {
                document = JsonDocument.Parse(output);
            }
            catch (JsonException exception)
            {
                throw new InvalidOperationException($"The tool \"{this._NameOfTheTool}\" declares an output-schema, so its answer has to be a json-document, which this answer is not: {exception.Message}", exception);
            }
            using (document)
            {
                ICollection<ValidationError> errors = this._Schema.Validate(output);
                if (0 < errors.Count)
                {
                    throw new InvalidOperationException($"The answer of the tool \"{this._NameOfTheTool}\" does not fit its output-schema: {string.Join(", ", errors.Select(Describe))}.");
                }
                // The document is only alive as long as it is not disposed, while the answer is handed on afterwards,
                // so what leaves here is a copy which belongs to nobody else.
                return document.RootElement.Clone();
            }
        }

        /// <summary>
        /// States one thing which did not fit, in the wording of a message to whoever called the tool: what is wrong
        /// and at which place of the answer it is. The value which was found there is not repeated, because the
        /// place and the reason are what tells an administrator which script has to be corrected.
        /// </summary>
        private static string Describe(ValidationError error)
        {
            return $"{error.Kind} at \"{error.Path}\"";
        }
    }
}
