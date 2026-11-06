using GenericMCPServerBackend.Core.Model;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace GenericMCPServerBackend.Core.Services
{
    /// <summary>
    /// Reads the tool-definitions-file and checks that every declaration in it is complete and unambiguous.
    ///
    /// Everything is checked while the server starts and not when a tool is called: a mistake in a declaration is a
    /// mistake of the configuration, and it has to be visible immediately instead of only when somebody happens to call
    /// the affected tool.
    /// </summary>
    public static class ToolDefinitionsReader
    {
        /// <summary>
        /// The form of a declared name. It is what a client sees, and it is also used as the name of the folder of the
        /// virtual environment of a python-declaration, so a name which contains a path-separator or similar is
        /// rejected here instead of turning into a folder somewhere else.
        /// </summary>
        private static readonly Regex NameRegex = new Regex("^[A-Za-z0-9_-]{1,128}$", RegexOptions.Compiled);

        /// <summary>
        /// The form of the name of a credential. Such a name becomes an environment-variable of the script, so it has
        /// to be usable as one.
        /// </summary>
        private static readonly Regex CredentialNameRegex = new Regex("^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.Compiled);

        /// <summary>
        /// The form of the namespace of an mcp-server: parts which are usable in an url, separated by "/".
        /// </summary>
        private static readonly Regex UrlNamespaceRegex = new Regex("^[A-Za-z0-9_-]+(/[A-Za-z0-9_-]+)*$", RegexOptions.Compiled);

        /// <summary>
        /// The form of the type of the content of a resource: a type and a subtype, as "text/markdown". A value which
        /// states more than that (a charset, for example) is rejected, because the content of a resource is handed
        /// over as text and such an addition would describe something which does not happen.
        /// </summary>
        private static readonly Regex MimeTypeRegex = new Regex(@"^[A-Za-z0-9][A-Za-z0-9.+-]*/[A-Za-z0-9][A-Za-z0-9.+-]*$", RegexOptions.Compiled);

        /// <summary>How a header refers to a credential of its own declaration.</summary>
        private static readonly Regex CredentialReferenceRegex = new Regex(@"\$\{([^}]*)\}", RegexOptions.Compiled);

        /// <summary>
        /// Reads the declared tools.
        /// </summary>
        /// <param name="file">The tool-definitions-file.</param>
        /// <exception cref="FileNotFoundException">Thrown when the file does not exist. That is an error and not an empty list of tools, because a configured-but-missing file is a mistake which would otherwise stay unnoticed.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the file is not a valid json-document or a declaration in it is incomplete, contradictory or ambiguous.</exception>
        public static IReadOnlyList<MCPServerDefinition> Read(string file)
        {
            if (!File.Exists(file))
            {
                throw new FileNotFoundException($"The tool-definitions-file \"{file}\" does not exist.", file);
            }
            ToolDefinitions definitions;
            try
            {
                definitions = JsonSerializer.Deserialize<ToolDefinitions>(File.ReadAllText(file), new JsonSerializerOptions() { ReadCommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
            }
            catch (JsonException exception)
            {
                throw new InvalidOperationException($"The tool-definitions-file \"{file}\" is not a valid json-document: {exception.Message}", exception);
            }
            List<MCPServerDefinition> result = new List<MCPServerDefinition>(definitions?.MCPServers ?? new List<MCPServerDefinition>());
            if (result.Count == 0 && 0 < (definitions?.ToolsOfTheFormerForm?.Count ?? 0))
            {
                // A file of the former form would otherwise silently declare nothing at all, because a tool now
                // belongs to an mcp-server.
                throw new InvalidOperationException($"The tool-definitions-file \"{file}\" declares tools directly. Every tool belongs to an mcp-server now, so the tools have to be moved into an entry of \"mcp-servers\" which states its \"url-namespace\".");
            }
            foreach (MCPServerDefinition mcpServer in result)
            {
                Validate(mcpServer, file);
            }
            string duplicatedNamespace = result.GroupBy(mcpServer => mcpServer.UrlNamespace, StringComparer.OrdinalIgnoreCase).Where(group => 1 < group.Count()).Select(group => group.Key).FirstOrDefault();
            if (duplicatedNamespace != null)
            {
                throw new InvalidOperationException($"The tool-definitions-file \"{file}\" declares the url-namespace \"{duplicatedNamespace}\" more than once, so it would not be decidable which mcp-server is meant by it.");
            }
            return result;
        }

        /// <summary>
        /// Checks one mcp-server: its namespace decides its address, so it has to be usable as a part of an url, and
        /// its tools, resources and prompts have to be unambiguous within it. Two mcp-servers may use the same
        /// tool-name, the same uri or the same prompt-name, because a client of one of them never sees the other one.
        /// </summary>
        private static void Validate(MCPServerDefinition mcpServer, string file)
        {
            if (string.IsNullOrWhiteSpace(mcpServer.UrlNamespace))
            {
                throw new InvalidOperationException($"The tool-definitions-file \"{file}\" contains an mcp-server without a url-namespace, which is what its address consists of.");
            }
            if (!UrlNamespaceRegex.IsMatch(mcpServer.UrlNamespace))
            {
                throw new InvalidOperationException($"The url-namespace \"{mcpServer.UrlNamespace}\" in \"{file}\" is not usable. It may consist of parts of letters, digits, underscores and hyphens which are separated by \"/\" (for example \"teamA\" or \"teamA/mcpserver1\").");
            }
            mcpServer.Tools ??= new List<ToolDefinition>();
            mcpServer.Resources ??= new List<ResourceDefinition>();
            mcpServer.Prompts ??= new List<PromptDefinition>();
            mcpServer.AuthorizedRoles ??= new List<string>();
            foreach (string role in mcpServer.AuthorizedRoles)
            {
                if (string.IsNullOrWhiteSpace(role))
                {
                    throw new InvalidOperationException($"The mcp-server \"{mcpServer.UrlNamespace}\" in \"{file}\" has an entry in \"authorized-roles\" without a name.");
                }
            }
            if (!mcpServer.Anonymous && mcpServer.AuthorizedRoles.Count == 0)
            {
                // Such an mcp-server could be used by nobody: it is not anonymous and no role may use it. That is a
                // mistake in the declaration and not a server which exists but answers nothing.
                throw new InvalidOperationException($"The mcp-server \"{mcpServer.UrlNamespace}\" in \"{file}\" states no \"authorized-roles\", so no client could use it. State the roles which may use it, or declare it as anonymous.");
            }
            foreach (ToolDefinition definition in mcpServer.Tools)
            {
                Validate(definition, file);
            }
            string duplicate = mcpServer.Tools.GroupBy(definition => definition.ToolName).Where(group => 1 < group.Count()).Select(group => group.Key).FirstOrDefault();
            if (duplicate != null)
            {
                throw new InvalidOperationException($"The mcp-server \"{mcpServer.UrlNamespace}\" in \"{file}\" declares the tool \"{duplicate}\" more than once.");
            }
            foreach (ResourceDefinition definition in mcpServer.Resources)
            {
                Validate(definition, file);
            }
            // A resource is read by its uri, so two resources with the same uri would make it undecidable which of
            // them a client means. Two resources with the same name are possible, because a name is what a reader
            // sees and not what identifies a resource.
            string duplicatedUri = mcpServer.Resources.GroupBy(definition => definition.Uri, StringComparer.Ordinal).Where(group => 1 < group.Count()).Select(group => group.Key).FirstOrDefault();
            if (duplicatedUri != null)
            {
                throw new InvalidOperationException($"The mcp-server \"{mcpServer.UrlNamespace}\" in \"{file}\" declares the uri \"{duplicatedUri}\" more than once, so it would not be decidable which resource is meant by it.");
            }
            foreach (PromptDefinition definition in mcpServer.Prompts)
            {
                Validate(definition, file);
            }
            string duplicatedPrompt = mcpServer.Prompts.GroupBy(definition => definition.PromptName).Where(group => 1 < group.Count()).Select(group => group.Key).FirstOrDefault();
            if (duplicatedPrompt != null)
            {
                throw new InvalidOperationException($"The mcp-server \"{mcpServer.UrlNamespace}\" in \"{file}\" declares the prompt \"{duplicatedPrompt}\" more than once.");
            }
        }

        private static void Validate(ToolDefinition definition, string file)
        {
            if (string.IsNullOrWhiteSpace(definition.ToolName))
            {
                throw new InvalidOperationException($"The tool-definitions-file \"{file}\" contains a tool without a name.");
            }
            if (!NameRegex.IsMatch(definition.ToolName))
            {
                throw new InvalidOperationException($"The name \"{definition.ToolName}\" in \"{file}\" is not a usable tool-name. A tool-name may consist of letters, digits, underscores and hyphens.");
            }
            ToolType type = ReadType(definition, file);
            switch (type)
            {
                case ToolType.Python:
                    ValidateScript(definition, file);
                    ValidateParameters(definition, file);
                    ValidateRequirements(definition, file);
                    ValidateCredentials(definition, file);
                    ValidateEnvironmentVariables(definition, file);
                    break;
                case ToolType.Shell:
                    ValidateScript(definition, file);
                    ValidateParameters(definition, file);
                    RejectDeclaredValues(definition.Requirements.Count, "requirements", definition, file, $"only a declaration of the type \"{ToolTypes.Python}\" installs requirements");
                    // A shell-tool receives no credential yet. Handing one over is a decision of its own, so it is
                    // stated here instead of being silently possible.
                    RejectDeclaredValues(definition.Credentials.Count, "credentials", definition, file, $"only a declaration of the type \"{ToolTypes.Python}\" or \"{ToolTypes.WebRequest}\" receives credentials");
                    ValidateEnvironmentVariables(definition, file);
                    break;
                case ToolType.WebRequest:
                    ValidateWebRequestTool(definition, file);
                    break;
            }
            // The output-schema belongs to the answer of the tool and not to the way that answer is produced, so it is
            // checked for every type and not per type.
            ValidateOutputSchema(definition, file);
        }

        /// <summary>
        /// Checks the output-schema of a tool, which states that the tool answers with a json-document of a known
        /// shape, and builds what checks the answers against it.
        ///
        /// A tool which states none answers with text alone, which is what every tool did before this existed: the
        /// schema is the one thing here which is optional, because declaring it is what a tool opts into.
        /// </summary>
        private static void ValidateOutputSchema(ToolDefinition definition, string file)
        {
            if (!definition.OutputSchema.HasValue)
            {
                return;
            }
            if (definition.OutputSchema.Value.ValueKind != JsonValueKind.Object)
            {
                // A json-schema which is a boolean allows everything respectively nothing, and neither describes an
                // answer. Anything else is no schema at all.
                throw new InvalidOperationException($"The tool \"{definition.ToolName}\" in \"{file}\" states an \"output-schema\" which is no json-object. Such a schema describes the answer of the tool, for example {{ \"type\": \"object\", \"properties\": {{ \"amount\": {{ \"type\": \"integer\" }} }} }}.");
            }
            try
            {
                definition.StructuredOutput = StructuredOutput.Create(definition.OutputSchema.Value, definition.ToolName);
            }
            catch (InvalidOperationException exception)
            {
                throw new InvalidOperationException($"The tool \"{definition.ToolName}\" in \"{file}\" states an \"output-schema\" which is not usable. {exception.Message}", exception);
            }
        }

        /// <summary>
        /// Rejects an output-schema which was declared for something which is no tool.
        ///
        /// Only a tool answers with a result whose shape a client reads as data: a resource states the type of its
        /// content, a prompt writes the text of the prompt and a completion writes one value per line. Ignoring such a
        /// declaration would let an administrator believe in an effect which does not exist.
        /// </summary>
        private static void RejectDeclaredOutputSchema(CallableDefinition definition, string file)
        {
            if (definition.OutputSchema.HasValue)
            {
                throw new InvalidOperationException($"The {definition.Kind} \"{definition.Name}\" in \"{file}\" states an \"output-schema\", which does not belong to it: only a tool answers with a result whose shape a client reads as data.");
            }
        }

        /// <summary>
        /// Checks one resource. It is declared like a tool and differs in what identifies it: a client reads it by its
        /// uri and sends nothing else, so a resource states that uri and the type of its content instead of
        /// parameters.
        /// </summary>
        private static void Validate(ResourceDefinition definition, string file)
        {
            if (string.IsNullOrWhiteSpace(definition.ResourceName))
            {
                throw new InvalidOperationException($"The tool-definitions-file \"{file}\" contains a resource without a name.");
            }
            if (!NameRegex.IsMatch(definition.ResourceName))
            {
                throw new InvalidOperationException($"The name \"{definition.ResourceName}\" in \"{file}\" is not a usable resource-name. A resource-name may consist of letters, digits, underscores and hyphens.");
            }
            ValidateUri(definition, file);
            if (string.IsNullOrWhiteSpace(definition.MimeType))
            {
                throw new InvalidOperationException($"The resource \"{definition.ResourceName}\" in \"{file}\" states no \"mime-type\", which is what tells a client what it received.");
            }
            if (!MimeTypeRegex.IsMatch(definition.MimeType))
            {
                throw new InvalidOperationException($"The resource \"{definition.ResourceName}\" in \"{file}\" states \"{definition.MimeType}\" as its \"mime-type\", which is not a usable one. Such a value consists of a type and a subtype, as \"text/markdown\".");
            }
            ToolType type = ReadType(definition, file);
            RejectDeclaredValues(definition.Parameters.Count, "parameters", definition, file, "a resource is read by its uri, so there is no value which a client could choose");
            RejectDeclaredOutputSchema(definition, file);
            switch (type)
            {
                case ToolType.Python:
                    ValidateScript(definition, file);
                    ValidateRequirements(definition, file);
                    ValidateCredentials(definition, file);
                    ValidateEnvironmentVariables(definition, file);
                    break;
                case ToolType.Shell:
                    ValidateScript(definition, file);
                    RejectDeclaredValues(definition.Requirements.Count, "requirements", definition, file, $"only a declaration of the type \"{ToolTypes.Python}\" installs requirements");
                    RejectDeclaredValues(definition.Credentials.Count, "credentials", definition, file, $"only a declaration of the type \"{ToolTypes.Python}\" or \"{ToolTypes.WebRequest}\" receives credentials");
                    ValidateEnvironmentVariables(definition, file);
                    break;
                case ToolType.WebRequest:
                    ValidateWebRequestResource(definition, file);
                    break;
            }
            if (type != ToolType.WebRequest)
            {
                // The script of a resource receives the uri of the resource it is called for, which is what lets one
                // script implement several resources.
                definition.Parameters = new List<string>() { ResourceDefinition.UriParameterName };
            }
        }

        /// <summary>
        /// Checks one prompt. It is declared like a tool: a client asks for it by its name and states the values of
        /// the arguments the declaration names, and what the script writes is the text of the prompt.
        /// </summary>
        private static void Validate(PromptDefinition definition, string file)
        {
            if (string.IsNullOrWhiteSpace(definition.PromptName))
            {
                throw new InvalidOperationException($"The tool-definitions-file \"{file}\" contains a prompt without a name.");
            }
            if (!NameRegex.IsMatch(definition.PromptName))
            {
                throw new InvalidOperationException($"The name \"{definition.PromptName}\" in \"{file}\" is not a usable prompt-name. A prompt-name may consist of letters, digits, underscores and hyphens.");
            }
            ToolType type = ReadType(definition, file);
            RejectDeclaredOutputSchema(definition, file);
            switch (type)
            {
                case ToolType.Python:
                    ValidateScript(definition, file);
                    ValidateParameters(definition, file);
                    ValidateRequirements(definition, file);
                    ValidateCredentials(definition, file);
                    ValidateEnvironmentVariables(definition, file);
                    break;
                case ToolType.Shell:
                    ValidateScript(definition, file);
                    ValidateParameters(definition, file);
                    RejectDeclaredValues(definition.Requirements.Count, "requirements", definition, file, $"only a declaration of the type \"{ToolTypes.Python}\" installs requirements");
                    RejectDeclaredValues(definition.Credentials.Count, "credentials", definition, file, $"only a declaration of the type \"{ToolTypes.Python}\" or \"{ToolTypes.WebRequest}\" receives credentials");
                    ValidateEnvironmentVariables(definition, file);
                    break;
                case ToolType.WebRequest:
                    // A declaration of that type requests an address, and that address comes either from the client
                    // (a tool, whose parameter "url" states it) or from the declaration itself (a resource, whose uri
                    // is it). A prompt has neither: its arguments are the values which are filled into its text, and
                    // it has no uri. Offering the type anyway would mean inventing one of those two for a prompt, so
                    // it is rejected here instead of behaving like one of them without saying which.
                    throw new InvalidOperationException($"The prompt \"{definition.PromptName}\" in \"{file}\" states the type \"{ToolTypes.WebRequest}\", which does not belong to a prompt: the arguments of a prompt are the values which are filled into its text, so there is no address which a client could ask for. A prompt is of the type \"{ToolTypes.Python}\" or \"{ToolTypes.Shell}\".");
            }
            ValidateCompletions(definition, file);
        }

        /// <summary>
        /// Checks what provides the possible values of the arguments of one prompt. Such a declaration stands under the
        /// name of the argument it completes, which has to be an argument that prompt actually takes: a provider for an
        /// argument which does not exist would be a script nobody ever calls.
        /// </summary>
        private static void ValidateCompletions(PromptDefinition prompt, string file)
        {
            prompt.Completions ??= new Dictionary<string, CompletionDefinition>();
            foreach (KeyValuePair<string, CompletionDefinition> completion in prompt.Completions)
            {
                if (completion.Value == null)
                {
                    throw new InvalidOperationException($"The prompt \"{prompt.PromptName}\" in \"{file}\" states a completion for the argument \"{completion.Key}\" without declaring what provides its values.");
                }
                completion.Value.PromptName = prompt.PromptName;
                completion.Value.ArgumentName = completion.Key;
                if (!NameRegex.IsMatch(completion.Key))
                {
                    // The name of the argument is a part of the name of the folder of the virtual environment of a
                    // python-completion, so a name which contains a path-separator is rejected here instead of turning
                    // into a folder somewhere else.
                    throw new InvalidOperationException($"The prompt \"{prompt.PromptName}\" in \"{file}\" states a completion for \"{completion.Key}\", which is not a usable name of an argument. Such a name may consist of letters, digits, underscores and hyphens.");
                }
                if (!prompt.Parameters.Contains(completion.Key, StringComparer.Ordinal))
                {
                    throw new InvalidOperationException($"The prompt \"{prompt.PromptName}\" in \"{file}\" states a completion for the argument \"{completion.Key}\", which this prompt does not declare in its \"parameters\".");
                }
                Validate(completion.Value, file);
            }
        }

        /// <summary>
        /// Checks one completion. It is declared like a prompt and differs in what it receives and in what a client
        /// does with its answer: it is told which argument it is asked about and which value was entered so far, and
        /// the lines it writes are offered as the possible values of that argument.
        /// </summary>
        private static void Validate(CompletionDefinition definition, string file)
        {
            ToolType type = ReadType(definition, file);
            RejectDeclaredValues(definition.Parameters.Count, "parameters", definition, file, "a completion receives the argument it is asked about and the value which was entered so far, so there is no parameter which could be declared");
            RejectDeclaredOutputSchema(definition, file);
            if (!string.IsNullOrWhiteSpace(definition.Description))
            {
                // A client never chooses a completion; it asks for the possible values of an argument it already
                // chose. A description would therefore be a text which nobody ever reads.
                throw new InvalidOperationException($"The completion \"{definition.Name}\" in \"{file}\" states a \"description\", which does not belong to it: a client asks for the possible values of an argument of a prompt and never chooses a completion itself, so there is nobody who would read it.");
            }
            switch (type)
            {
                case ToolType.Python:
                    ValidateScript(definition, file);
                    ValidateRequirements(definition, file);
                    ValidateCredentials(definition, file);
                    ValidateEnvironmentVariables(definition, file);
                    break;
                case ToolType.Shell:
                    ValidateScript(definition, file);
                    RejectDeclaredValues(definition.Requirements.Count, "requirements", definition, file, $"only a declaration of the type \"{ToolTypes.Python}\" installs requirements");
                    RejectDeclaredValues(definition.Credentials.Count, "credentials", definition, file, $"only a declaration of the type \"{ToolTypes.Python}\" or \"{ToolTypes.WebRequest}\" receives credentials");
                    ValidateEnvironmentVariables(definition, file);
                    break;
                case ToolType.WebRequest:
                    // Such a declaration takes the address it requests from the client (a tool) or from its own uri (a
                    // resource), and a completion has neither: it is asked which values one argument of one prompt can
                    // have. Offering the type anyway would mean inventing one of those two for it.
                    throw new InvalidOperationException($"The completion \"{definition.Name}\" in \"{file}\" states the type \"{ToolTypes.WebRequest}\", which does not belong to a completion: it is asked which values an argument of a prompt can have, so there is no address which a client could ask for. A completion is of the type \"{ToolTypes.Python}\" or \"{ToolTypes.Shell}\".");
            }
            // The script is told which argument it is asked about and which value was entered so far, which is what
            // lets one script complete several arguments.
            definition.Parameters = new List<string>() { CompletionDefinition.ArgumentParameterName, CompletionDefinition.ValueParameterName };
        }

        /// <summary>
        /// Checks the uri of a resource: it is what a client reads the resource by, so it has to be one which a client
        /// can send.
        /// </summary>
        private static void ValidateUri(ResourceDefinition definition, string file)
        {
            if (string.IsNullOrWhiteSpace(definition.Uri))
            {
                throw new InvalidOperationException($"The resource \"{definition.ResourceName}\" in \"{file}\" states no \"uri\", which is what a client reads it by.");
            }
            if (definition.Uri.Contains('{') || definition.Uri.Contains('}'))
            {
                // Such a value would be a uri-template, which is a resource whose uri a client fills in. This server
                // offers the resources which are declared and nothing which is derived from them.
                throw new InvalidOperationException($"The uri \"{definition.Uri}\" of the resource \"{definition.ResourceName}\" in \"{file}\" contains a template-expression. A resource is declared with the uri it has.");
            }
            if (!Uri.TryCreate(definition.Uri, UriKind.Absolute, out Uri _))
            {
                throw new InvalidOperationException($"The uri \"{definition.Uri}\" of the resource \"{definition.ResourceName}\" in \"{file}\" is not usable. A resource is read by an absolute uri (for example \"https://example.com/handbook\" or \"file:///documents/handbook.md\").");
            }
        }

        /// <summary>
        /// Reads the declared type, which every declaration has to state, and makes sure that every list and every
        /// map of it exists.
        /// </summary>
        private static ToolType ReadType(CallableDefinition definition, string file)
        {
            definition.Parameters ??= new List<string>();
            definition.Requirements ??= new List<string>();
            definition.AllowedUrls ??= new List<string>();
            definition.Credentials ??= new Dictionary<string, string>();
            definition.Headers ??= new Dictionary<string, string>();
            if (string.IsNullOrWhiteSpace(definition.Type))
            {
                throw new InvalidOperationException($"The {definition.Kind} \"{definition.Name}\" in \"{file}\" does not state its type. The possible types are {ToolTypes.AllowedValues}.");
            }
            if (!ToolTypes.TryParse(definition.Type, out ToolType result))
            {
                throw new InvalidOperationException($"The {definition.Kind} \"{definition.Name}\" in \"{file}\" states the unknown type \"{definition.Type}\". The possible types are {ToolTypes.AllowedValues}.");
            }
            return result;
        }

        /// <summary>
        /// Checks a declaration which runs a script: it has to state which one, and it must not state what only a
        /// declaration which requests an address has.
        /// </summary>
        private static void ValidateScript(CallableDefinition definition, string file)
        {
            if (string.IsNullOrWhiteSpace(definition.Calls))
            {
                throw new InvalidOperationException($"The {definition.Kind} \"{definition.Name}\" in \"{file}\" does not state which script it calls.");
            }
            RejectDeclaredValues(definition.AllowedUrls.Count, "allowed-urls", definition, file, $"only a declaration of the type \"{ToolTypes.WebRequest}\" requests addresses");
            RejectDeclaredValues(definition.Headers.Count, "headers", definition, file, $"only a declaration of the type \"{ToolTypes.WebRequest}\" sends headers");
        }

        /// <summary>
        /// Checks the parameters of a declaration which takes values of the client: a tool, which is called with them,
        /// and a prompt, whose text they are filled into.
        /// </summary>
        private static void ValidateParameters(CallableDefinition definition, string file)
        {
            foreach (string parameter in definition.Parameters)
            {
                if (string.IsNullOrWhiteSpace(parameter))
                {
                    throw new InvalidOperationException($"The {definition.Kind} \"{definition.Name}\" in \"{file}\" has a parameter without a name.");
                }
            }
        }

        /// <summary>
        /// Checks the credentials of a declaration. The name they are seen under becomes an environment-variable
        /// respectively the name a header refers to, and the other one is the name of an environment-variable of the
        /// server; whether that variable exists is decided when the server starts (see
        /// <see cref="CredentialsService"/>).
        /// </summary>
        private static void ValidateCredentials(CallableDefinition definition, string file)
        {
            foreach (KeyValuePair<string, string> credential in definition.Credentials)
            {
                if (!CredentialNameRegex.IsMatch(credential.Key ?? string.Empty))
                {
                    throw new InvalidOperationException($"The {definition.Kind} \"{definition.Name}\" in \"{file}\" has the credential \"{credential.Key}\", whose name is not usable. Such a name may consist of letters, digits and underscores and may not begin with a digit.");
                }
                if (!CredentialNameRegex.IsMatch(credential.Value ?? string.Empty))
                {
                    throw new InvalidOperationException($"The credential \"{credential.Key}\" of the {definition.Kind} \"{definition.Name}\" in \"{file}\" states \"{credential.Value}\" as the environment-variable it is read from, which is not a usable name of an environment-variable.");
                }
            }
        }

        /// <summary>
        /// Checks the environment-variables a declaration hands to its script. Each name becomes an
        /// environment-variable of the script, so it has to be usable as one; a name may not be stated as mandatory and
        /// as optional at once, because that would be two contradicting statements about the same variable; and it may
        /// not collide with a credential, which is set under the same kind of name and would otherwise silently win.
        /// </summary>
        private static void ValidateEnvironmentVariables(CallableDefinition definition, string file)
        {
            foreach (string name in definition.MandatoryEnvironmentVariables.Concat(definition.OptionalEnvironmentVariables))
            {
                if (!CredentialNameRegex.IsMatch(name ?? string.Empty))
                {
                    throw new InvalidOperationException($"The {definition.Kind} \"{definition.Name}\" in \"{file}\" declares the environment-variable \"{name}\", whose name is not usable. Such a name may consist of letters, digits and underscores and may not begin with a digit.");
                }
            }
            string statedAsBoth = definition.MandatoryEnvironmentVariables.Intersect(definition.OptionalEnvironmentVariables, StringComparer.Ordinal).FirstOrDefault();
            if (statedAsBoth != null)
            {
                throw new InvalidOperationException($"The {definition.Kind} \"{definition.Name}\" in \"{file}\" states the environment-variable \"{statedAsBoth}\" as mandatory and as optional at once, so it would not be decidable whether it has to be set.");
            }
            string collidesWithACredential = definition.MandatoryEnvironmentVariables.Concat(definition.OptionalEnvironmentVariables).Intersect(definition.Credentials.Keys, StringComparer.Ordinal).FirstOrDefault();
            if (collidesWithACredential != null)
            {
                throw new InvalidOperationException($"The {definition.Kind} \"{definition.Name}\" in \"{file}\" states \"{collidesWithACredential}\" both as an environment-variable and as a credential, which the script would see under the same name.");
            }
        }

        /// <summary>
        /// Rejects environment-variables which were declared for a type which runs no script. A web-request has no
        /// script which an environment-variable could reach, so stating one is a misunderstanding and not something
        /// which is silently ignored.
        /// </summary>
        private static void RejectEnvironmentVariables(CallableDefinition definition, string file)
        {
            RejectDeclaredValues(definition.MandatoryEnvironmentVariables.Count, "mandatory-env-variables", definition, file, $"only a declaration of the type \"{ToolTypes.Python}\" or \"{ToolTypes.Shell}\" runs a script which receives environment-variables");
            RejectDeclaredValues(definition.OptionalEnvironmentVariables.Count, "optional-env-variables", definition, file, $"only a declaration of the type \"{ToolTypes.Python}\" or \"{ToolTypes.Shell}\" runs a script which receives environment-variables");
        }

        /// <summary>
        /// Checks the headers of a declaration. A header may refer to a credential of its own declaration, and only to
        /// one of them: a reference which points at nothing would otherwise become a request whose header is
        /// incomplete without anybody noticing.
        /// </summary>
        private static void ValidateHeaders(CallableDefinition definition, string file)
        {
            foreach (KeyValuePair<string, string> header in definition.Headers)
            {
                if (string.IsNullOrWhiteSpace(header.Key))
                {
                    throw new InvalidOperationException($"The {definition.Kind} \"{definition.Name}\" in \"{file}\" has a header without a name.");
                }
                foreach (Match reference in CredentialReferenceRegex.Matches(header.Value ?? string.Empty))
                {
                    string nameOfTheCredential = reference.Groups[1].Value;
                    if (!definition.Credentials.ContainsKey(nameOfTheCredential))
                    {
                        throw new InvalidOperationException($"The header \"{header.Key}\" of the {definition.Kind} \"{definition.Name}\" in \"{file}\" refers to the credential \"{nameOfTheCredential}\", which this {definition.Kind} does not declare.");
                    }
                }
            }
        }

        private static void ValidateRequirements(CallableDefinition definition, string file)
        {
            foreach (string requirement in definition.Requirements)
            {
                if (string.IsNullOrWhiteSpace(requirement))
                {
                    throw new InvalidOperationException($"The {definition.Kind} \"{definition.Name}\" in \"{file}\" has an empty requirement.");
                }
            }
        }

        /// <summary>
        /// Checks a tool which requests an address instead of running a script. Such a tool declares neither a script
        /// nor its parameters: it takes exactly the one parameter which the mechanism defines.
        /// </summary>
        private static void ValidateWebRequestTool(ToolDefinition definition, string file)
        {
            RejectDeclaredScript(definition, file);
            RejectDeclaredValues(definition.Parameters.Count, "parameters", definition, file, $"a tool of the type \"{ToolTypes.WebRequest}\" has exactly the parameter \"{WebRequestService.UrlParameterName}\"");
            RejectDeclaredValues(definition.Requirements.Count, "requirements", definition, file, $"only a declaration of the type \"{ToolTypes.Python}\" installs requirements");
            RejectEnvironmentVariables(definition, file);
            if (definition.AllowedUrls.Count == 0)
            {
                throw new InvalidOperationException($"The tool \"{definition.ToolName}\" in \"{file}\" allows no address. Everything which no rule allows is denied, so this tool could request nothing at all.");
            }
            foreach (string rule in definition.AllowedUrls)
            {
                if (string.IsNullOrWhiteSpace(rule))
                {
                    throw new InvalidOperationException($"The tool \"{definition.ToolName}\" in \"{file}\" has an empty rule in \"allowed-urls\".");
                }
                try
                {
                    _ = Regex.Match(string.Empty, "^(?:" + rule + ")$");
                }
                catch (ArgumentException exception)
                {
                    throw new InvalidOperationException($"The rule \"{rule}\" of the tool \"{definition.ToolName}\" in \"{file}\" is not a valid regular expression: {exception.Message}", exception);
                }
            }
            definition.Parameters = new List<string>() { WebRequestService.UrlParameterName };
            ValidateCredentials(definition, file);
            ValidateHeaders(definition, file);
        }

        /// <summary>
        /// Checks a resource which is requested from an address. Such a resource states no rules for the addresses it
        /// allows: nothing which a client sends decides what is requested, because the address is the uri of the
        /// resource itself.
        /// </summary>
        private static void ValidateWebRequestResource(ResourceDefinition definition, string file)
        {
            RejectDeclaredScript(definition, file);
            RejectDeclaredValues(definition.Requirements.Count, "requirements", definition, file, $"only a declaration of the type \"{ToolTypes.Python}\" installs requirements");
            RejectDeclaredValues(definition.AllowedUrls.Count, "allowed-urls", definition, file, $"a resource of the type \"{ToolTypes.WebRequest}\" is requested from its own uri, so there is no address which a client could ask for");
            RejectEnvironmentVariables(definition, file);
            if (!definition.Uri.StartsWith($"{WebRequestService.Protocol}://", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"The resource \"{definition.ResourceName}\" in \"{file}\" is of the type \"{ToolTypes.WebRequest}\", so its uri \"{definition.Uri}\" is the address which is requested and has to state the protocol \"{WebRequestService.Protocol}\".");
            }
            ValidateCredentials(definition, file);
            ValidateHeaders(definition, file);
        }

        /// <summary>
        /// Rejects a script which was declared although the declaration requests an address.
        /// </summary>
        private static void RejectDeclaredScript(CallableDefinition definition, string file)
        {
            if (!string.IsNullOrWhiteSpace(definition.Calls))
            {
                throw new InvalidOperationException($"The {definition.Kind} \"{definition.Name}\" in \"{file}\" states a script, but a declaration of the type \"{ToolTypes.WebRequest}\" runs no script.");
            }
        }

        /// <summary>
        /// Rejects a value which was declared although it has no meaning for the type of the declaration. Such a
        /// declaration is not ignored, because ignoring it would let an administrator believe in an effect which does
        /// not exist.
        /// </summary>
        private static void RejectDeclaredValues(int amountOfDeclaredValues, string nameOfTheField, CallableDefinition definition, string file, string reason)
        {
            if (0 < amountOfDeclaredValues)
            {
                throw new InvalidOperationException($"The {definition.Kind} \"{definition.Name}\" in \"{file}\" is of the type \"{definition.Type}\" and states \"{nameOfTheField}\", which does not belong to it: {reason}.");
            }
        }
    }
}
