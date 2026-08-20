using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace GenericMCPServerBackend.Core.Services
{
    /// <summary>
    /// Holds the credentials which the declared tools need.
    ///
    /// A credential belongs to the administrator who runs the server and never to the client, so it is not a value of a
    /// parameter but something the server is configured with: the declaration of a tool states the name of an
    /// environment-variable of the server, and this service reads it.
    ///
    /// Everything is read when the server starts. A credential which is missing is therefore a reason not to start at
    /// all, which makes a forgotten secret visible while deploying instead of when a model happens to call the affected
    /// tool.
    /// </summary>
    public sealed class CredentialsService
    {
        /// <summary>
        /// The suffix of the variable which states the file a value is read from instead of the value itself. This is
        /// the usual way of handing a secret to a container (docker-secrets are files), and it keeps the secret out of
        /// the compose-file.
        /// </summary>
        public const string NameSuffixForAFile = "_FILE";

        /// <summary>What is written instead of a value which was removed from a text.</summary>
        private const string Replacement = "***";

        /// <summary>The values, keyed by the name of the variable they were read from.</summary>
        private readonly IReadOnlyDictionary<string, string> _Values;

        /// <summary>
        /// Reads the given credentials.
        /// </summary>
        /// <param name="names">The names of the environment-variables the declarations refer to.</param>
        /// <exception cref="InvalidOperationException">Thrown when a credential is not available. The message contains the name and never the value.</exception>
        public CredentialsService(IEnumerable<string> names)
        {
            Dictionary<string, string> values = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (string name in names.Distinct(StringComparer.Ordinal))
            {
                values[name] = Read(name);
            }
            this._Values = values;
        }

        /// <exception cref="InvalidOperationException">Thrown when neither the variable nor the file it points to provides a value.</exception>
        private static string Read(string name)
        {
            string value = Environment.GetEnvironmentVariable(name);
            if (!string.IsNullOrEmpty(value))
            {
                return value;
            }
            string nameOfTheFileVariable = name + NameSuffixForAFile;
            string file = Environment.GetEnvironmentVariable(nameOfTheFileVariable);
            if (!string.IsNullOrWhiteSpace(file))
            {
                if (!File.Exists(file))
                {
                    throw new InvalidOperationException($"The credential \"{name}\" is read from the file \"{file}\" which \"{nameOfTheFileVariable}\" states, but that file does not exist.");
                }
                // The trailing line-break which an editor or a secret-manager adds is not part of the secret; a value
                // which kept it would be sent as-is and would be rejected by the foreign service for a reason nobody
                // can see.
                string content = File.ReadAllText(file).Trim();
                if (!string.IsNullOrEmpty(content))
                {
                    return content;
                }
                throw new InvalidOperationException($"The credential \"{name}\" is read from the file \"{file}\" which \"{nameOfTheFileVariable}\" states, but that file is empty.");
            }
            throw new InvalidOperationException($"The credential \"{name}\" is declared by a tool but is not available: neither the environment-variable \"{name}\" nor \"{nameOfTheFileVariable}\" is set.");
        }

        /// <summary>
        /// The names of all variables the credentials are read from. They are removed from the environment of every
        /// script which is started, so that a tool can not read a credential which belongs to another tool.
        /// </summary>
        public IReadOnlyCollection<string> GetNames()
        {
            return (IReadOnlyCollection<string>)this._Values.Keys;
        }

        /// <exception cref="KeyNotFoundException">Thrown when no credential of that name was read. That can not happen for a declared one, because every declared name is read while starting.</exception>
        public string GetValue(string name)
        {
            return this._Values[name];
        }

        /// <summary>
        /// Removes every known value from the given text.
        ///
        /// The message of a failed tool-call is handed to the client, so a script which writes its credential into a
        /// stacktrace would hand it over as well. This does not replace the care which a script has to take, but it
        /// keeps the most common accident from becoming a leak.
        /// </summary>
        public string RemoveValuesFrom(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return text;
            }
            string result = text;
            foreach (string value in this._Values.Values)
            {
                if (!string.IsNullOrEmpty(value))
                {
                    result = result.Replace(value, Replacement, StringComparison.Ordinal);
                }
            }
            return result;
        }
    }
}
