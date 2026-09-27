namespace GenericMCPServerBackend.Core.Constants
{
    /// <summary>
    /// Contains general constants of this codeunit.
    /// </summary>
    internal static class GeneralConstants
    {
        /// <summary>
        /// The name of this codeunit.
        /// </summary>
        internal const string CodeUnitName = "GenericMCPServerBackend";

        /// <summary>
        /// The description of this codeunit.
        /// </summary>
        internal const string CodeUnitDescription = "GenericMCPServerBackend is the backend for GenericMCPServer.";

        /// <summary>
        /// The version of this codeunit.
        /// </summary>
        internal const string CodeUnitVersion = "1.0.0";

        /// <summary>
        /// The name of the folder, relative to the base-folder of the server, which contains the declaration of the mcp-servers and the scripts it names.
        /// It lies next to the configuration-, the data- and the log-folder and not inside one of them, because its content belongs to the administrator
        /// and not to the server.
        /// </summary>
        internal const string ToolsFolderName = "Tools";

        /// <summary>
        /// The name which the tool-definitions-file has in the tools-folder when nothing else was stated while the configuration-file was generated. What
        /// is written into that file is the whole path of it and not this name, so an administrator reads there where the declaration is looked for.
        /// </summary>
        internal const string DefaultToolDefinitionsFileName = "Tools.json";
    }
}
