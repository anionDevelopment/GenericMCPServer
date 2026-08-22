using System;

namespace GenericMCPServerBackend.Core.Model
{
    /// <summary>
    /// The kind of script which implements a tool.
    ///
    /// The type has to be declared, it is not guessed from the file-extension: which interpreter runs a script decides
    /// what the script may do, so this is a decision of the administrator and not one which follows from the name of a
    /// file which somebody may have chosen for another reason.
    /// </summary>
    public enum ToolType
    {
        /// <summary>A python-script. It is run in a virtual environment which belongs to the tool.</summary>
        Python,

        /// <summary>A shell-script. It is run with the shell of the system, which requires a linux-system.</summary>
        Shell,

        /// <summary>
        /// No script at all: the tool requests an address which the client states, and the declaration decides which
        /// addresses it may request.
        /// </summary>
        WebRequest
    }

    /// <summary>
    /// The values which are allowed for the type of a tool and how they are read.
    /// </summary>
    public static class ToolTypes
    {
        /// <summary>The declared value for <see cref="ToolType.Python"/>.</summary>
        public const string Python = "python";

        /// <summary>The declared value for <see cref="ToolType.Shell"/>.</summary>
        public const string Shell = "shell";

        /// <summary>The declared value for <see cref="ToolType.WebRequest"/>.</summary>
        public const string WebRequest = "web-request";

        /// <summary>The allowed values, formatted for a message which tells an administrator what is possible.</summary>
        public static string AllowedValues => $"\"{Python}\", \"{Shell}\" and \"{WebRequest}\"";

        /// <summary>
        /// Reads the declared type. The comparison ignores the case, because a type which is written with a capital
        /// letter is a typo and not a different type.
        /// </summary>
        /// <returns>True if the value is one of the allowed ones.</returns>
        public static bool TryParse(string value, out ToolType result)
        {
            if (string.Equals(value, Python, StringComparison.OrdinalIgnoreCase))
            {
                result = ToolType.Python;
                return true;
            }
            if (string.Equals(value, Shell, StringComparison.OrdinalIgnoreCase))
            {
                result = ToolType.Shell;
                return true;
            }
            if (string.Equals(value, WebRequest, StringComparison.OrdinalIgnoreCase))
            {
                result = ToolType.WebRequest;
                return true;
            }
            result = default;
            return false;
        }
    }
}
