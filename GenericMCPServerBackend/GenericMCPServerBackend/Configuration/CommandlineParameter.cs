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
        /// The file which declares the tools this server offers. Without it the server offers no tool at all, which is
        /// deliberate: a tool exists because an administrator declared it and not because this codeunit brings it
        /// along.
        /// </summary>
        [Option(nameof(InitialToolDefinitionsFile), Required = false)]
        public string? InitialToolDefinitionsFile { get; set; }

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
