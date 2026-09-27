using System.Collections.Generic;
using System.Text.Json;

namespace GenericMCPServerBackend.Core.Misc
{
    /// <summary>
    /// Reads the arguments which a client sent with a request of the protocol.
    ///
    /// A value is handed to a script as text, so every argument is turned into one here. This exists once and is used
    /// by every adapter which passes arguments on (<see cref="ScriptTool"/> and <see cref="ScriptPrompt"/>), because a
    /// second implementation of it could read the same message of a client differently than the first one.
    /// </summary>
    internal static class CallArguments
    {
        /// <param name="arguments">The arguments of the request, or null when the request carried none.</param>
        /// <returns>The value of every argument as text, keyed by the name of the argument.</returns>
        internal static Dictionary<string, string> Read(IEnumerable<KeyValuePair<string, JsonElement>> arguments)
        {
            Dictionary<string, string> result = new Dictionary<string, string>();
            if (arguments != null)
            {
                foreach (KeyValuePair<string, JsonElement> argument in arguments)
                {
                    // A value which is not a json-string is passed on in its json-form, so that a client which sends a
                    // number does not get an error about a missing argument.
                    result[argument.Key] = argument.Value.ValueKind == JsonValueKind.String ? argument.Value.GetString() : argument.Value.GetRawText();
                }
            }
            return result;
        }
    }
}
