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
        /// The form of a tool-name. It is what a client sees, and it is also used as the name of the folder of the
        /// virtual environment of a python-tool, so a name which contains a path-separator or similar is rejected here
        /// instead of turning into a folder somewhere else.
        /// </summary>
        private static readonly Regex ToolNameRegex = new Regex("^[A-Za-z0-9_-]{1,128}$", RegexOptions.Compiled);

        /// <summary>
        /// The form of the name of a credential. Such a name becomes an environment-variable of the script, so it has
        /// to be usable as one.
        /// </summary>
        private static readonly Regex CredentialNameRegex = new Regex("^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.Compiled);

        /// <summary>
        /// The form of the namespace of an mcp-server: parts which are usable in an url, separated by "/".
        /// </summary>
        private static readonly Regex UrlNamespaceRegex = new Regex("^[A-Za-z0-9_-]+(/[A-Za-z0-9_-]+)*$", RegexOptions.Compiled);

        /// <summary>How a header refers to a credential of its tool.</summary>
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
        /// its tools have to be unambiguous within it. Two mcp-servers may use the same tool-name, because a client of
        /// one of them never sees the other one.
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
        }

        private static void Validate(ToolDefinition definition, string file)
        {
            if (string.IsNullOrWhiteSpace(definition.ToolName))
            {
                throw new InvalidOperationException($"The tool-definitions-file \"{file}\" contains a tool without a name.");
            }
            if (!ToolNameRegex.IsMatch(definition.ToolName))
            {
                throw new InvalidOperationException($"The name \"{definition.ToolName}\" in \"{file}\" is not a usable tool-name. A tool-name may consist of letters, digits, underscores and hyphens.");
            }
            definition.Parameters ??= new List<string>();
            definition.Requirements ??= new List<string>();
            definition.AllowedUrls ??= new List<string>();
            definition.Credentials ??= new Dictionary<string, string>();
            definition.Headers ??= new Dictionary<string, string>();
            if (string.IsNullOrWhiteSpace(definition.Type))
            {
                throw new InvalidOperationException($"The tool \"{definition.ToolName}\" in \"{file}\" does not state its type. The possible types are {ToolTypes.AllowedValues}.");
            }
            if (!ToolTypes.TryParse(definition.Type, out ToolType type))
            {
                throw new InvalidOperationException($"The tool \"{definition.ToolName}\" in \"{file}\" states the unknown type \"{definition.Type}\". The possible types are {ToolTypes.AllowedValues}.");
            }
            switch (type)
            {
                case ToolType.Python:
                    ValidateScriptTool(definition, file);
                    ValidateRequirements(definition, file);
                    ValidateCredentials(definition, file);
                    break;
                case ToolType.Shell:
                    ValidateScriptTool(definition, file);
                    RejectDeclaredValues(definition.Requirements.Count, "requirements", definition, file, $"only a tool of the type \"{ToolTypes.Python}\" installs requirements");
                    // A shell-tool receives no credential yet. Handing one over is a decision of its own, so it is
                    // stated here instead of being silently possible.
                    RejectDeclaredValues(definition.Credentials.Count, "credentials", definition, file, $"only a tool of the type \"{ToolTypes.Python}\" or \"{ToolTypes.WebRequest}\" receives credentials");
                    break;
                case ToolType.WebRequest:
                    ValidateWebRequestTool(definition, file);
                    break;
            }
        }

        private static void ValidateScriptTool(ToolDefinition definition, string file)
        {
            if (string.IsNullOrWhiteSpace(definition.Calls))
            {
                throw new InvalidOperationException($"The tool \"{definition.ToolName}\" in \"{file}\" does not state which script it calls.");
            }
            foreach (string parameter in definition.Parameters)
            {
                if (string.IsNullOrWhiteSpace(parameter))
                {
                    throw new InvalidOperationException($"The tool \"{definition.ToolName}\" in \"{file}\" has a parameter without a name.");
                }
            }
            RejectDeclaredValues(definition.AllowedUrls.Count, "allowed-urls", definition, file, $"only a tool of the type \"{ToolTypes.WebRequest}\" requests addresses");
            RejectDeclaredValues(definition.Headers.Count, "headers", definition, file, $"only a tool of the type \"{ToolTypes.WebRequest}\" sends headers");
        }

        /// <summary>
        /// Checks the credentials of a tool. The name the tool sees them under becomes an environment-variable
        /// respectively the name a header refers to, and the other one is the name of an environment-variable of the
        /// server; whether that variable exists is decided when the server starts (see
        /// <see cref="CredentialsService"/>).
        /// </summary>
        private static void ValidateCredentials(ToolDefinition definition, string file)
        {
            foreach (KeyValuePair<string, string> credential in definition.Credentials)
            {
                if (!CredentialNameRegex.IsMatch(credential.Key ?? string.Empty))
                {
                    throw new InvalidOperationException($"The tool \"{definition.ToolName}\" in \"{file}\" has the credential \"{credential.Key}\", whose name is not usable. Such a name may consist of letters, digits and underscores and may not begin with a digit.");
                }
                if (!CredentialNameRegex.IsMatch(credential.Value ?? string.Empty))
                {
                    throw new InvalidOperationException($"The credential \"{credential.Key}\" of the tool \"{definition.ToolName}\" in \"{file}\" states \"{credential.Value}\" as the environment-variable it is read from, which is not a usable name of an environment-variable.");
                }
            }
        }

        /// <summary>
        /// Checks the headers of a tool. A header may refer to a credential of its own tool, and only to one of them:
        /// a reference which points at nothing would otherwise become a request whose header is incomplete without
        /// anybody noticing.
        /// </summary>
        private static void ValidateHeaders(ToolDefinition definition, string file)
        {
            foreach (KeyValuePair<string, string> header in definition.Headers)
            {
                if (string.IsNullOrWhiteSpace(header.Key))
                {
                    throw new InvalidOperationException($"The tool \"{definition.ToolName}\" in \"{file}\" has a header without a name.");
                }
                foreach (Match reference in CredentialReferenceRegex.Matches(header.Value ?? string.Empty))
                {
                    string nameOfTheCredential = reference.Groups[1].Value;
                    if (!definition.Credentials.ContainsKey(nameOfTheCredential))
                    {
                        throw new InvalidOperationException($"The header \"{header.Key}\" of the tool \"{definition.ToolName}\" in \"{file}\" refers to the credential \"{nameOfTheCredential}\", which this tool does not declare.");
                    }
                }
            }
        }

        private static void ValidateRequirements(ToolDefinition definition, string file)
        {
            foreach (string requirement in definition.Requirements)
            {
                if (string.IsNullOrWhiteSpace(requirement))
                {
                    throw new InvalidOperationException($"The tool \"{definition.ToolName}\" in \"{file}\" has an empty requirement.");
                }
            }
        }

        /// <summary>
        /// Checks a tool which requests an address instead of running a script. Such a tool declares neither a script
        /// nor its parameters: it takes exactly the one parameter which the mechanism defines.
        /// </summary>
        private static void ValidateWebRequestTool(ToolDefinition definition, string file)
        {
            if (!string.IsNullOrWhiteSpace(definition.Calls))
            {
                throw new InvalidOperationException($"The tool \"{definition.ToolName}\" in \"{file}\" states a script, but a tool of the type \"{ToolTypes.WebRequest}\" runs no script.");
            }
            RejectDeclaredValues(definition.Parameters.Count, "parameters", definition, file, $"a tool of the type \"{ToolTypes.WebRequest}\" has exactly the parameter \"{WebRequestService.UrlParameterName}\"");
            RejectDeclaredValues(definition.Requirements.Count, "requirements", definition, file, $"only a tool of the type \"{ToolTypes.Python}\" installs requirements");
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
                    _ = Regex.Match(string.Empty, "^" + rule + "$");
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
        /// Rejects a value which was declared although it has no meaning for the type of the tool. Such a declaration
        /// is not ignored, because ignoring it would let an administrator believe in an effect which does not exist.
        /// </summary>
        private static void RejectDeclaredValues(int amountOfDeclaredValues, string nameOfTheField, ToolDefinition definition, string file, string reason)
        {
            if (0 < amountOfDeclaredValues)
            {
                throw new InvalidOperationException($"The tool \"{definition.ToolName}\" in \"{file}\" is of the type \"{definition.Type}\" and states \"{nameOfTheField}\", which does not belong to it: {reason}.");
            }
        }
    }
}
