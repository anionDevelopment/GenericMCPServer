using CommandLine;
using GRYLibrary.Core.APIServer.Verbs;

namespace GenericMCPServerBackend.Core.Configuration
{
    /// <summary>
    /// Commandline-parameters supported by this codeunit. There are no parameters for a database or for an initial
    /// user, because this codeunit needs neither of them.
    /// </summary>
    public class CommandlineParameter : RunServer
    {
        [Option(nameof(InitialDomain), Required = false)]
        public string? InitialDomain { get; set; }

        /// <summary>
        /// The value which <see cref="CodeUnitSpecificConfiguration.ToolDefinitionsFile"/> gets the first time the
        /// configuration-file is generated, instead of the file "Tools.json" in the tools-folder of the server. It is
        /// the whole path of that file including its name and not the folder it lies in - which is what lets a container
        /// receive the declaration as a single file mounted into the tools-folder instead of as a whole mounted folder.
        ///
        /// It decides the initial value and nothing else: from the second start on the configuration-file is what the
        /// server reads, so stating this afterwards changes nothing and an administrator who wants the declaration
        /// somewhere else edits that file.
        /// </summary>
        [Option(nameof(InitialToolsDefinitionFilePath), Required = false)]
        public string? InitialToolsDefinitionFilePath { get; set; }

        [Option(nameof(InitialEnableEndpointAvailabilityCheckValue), Required = false, Default = true)]
        public bool InitialEnableEndpointAvailabilityCheckValue { get; set; }

        [Option(nameof(InitialEnableEndpointInitializationStateValue), Required = false, Default = false)]
        public bool InitialEnableEndpointInitializationStateValue { get; set; }

        [Option(nameof(InitialEnableEndpointCurrentVersionValue), Required = false, Default = false)]
        public bool InitialEnableEndpointCurrentVersionValue { get; set; }

        [Option(nameof(InitialEnableEndpointShowAllEndpointsValue), Required = false, Default = false)]
        public bool InitialEnableEndpointShowAllEndpointsValue { get; set; }

        [Option(nameof(InitialEnableEndpointHealthCheckValue), Required = false, Default = true)]
        public bool InitialEnableEndpointHealthCheckValue { get; set; }

        [Option(nameof(InitialEnableEndpointMetricsValue), Required = false, Default = false)]
        public bool InitialEnableEndpointMetricsValue { get; set; }
    }
}
