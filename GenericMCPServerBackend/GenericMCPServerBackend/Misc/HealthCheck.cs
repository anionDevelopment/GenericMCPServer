using GenericMCPServerBackend.Core.Configuration;
using GRYLibrary.Core.APIServer.Services.Init;
using GRYLibrary.Core.APIServer.Services.Logger;
using GRYLibrary.Core.APIServer.Utilities;
using GRYLibrary.Core.Logging.GeneralPurposeLogger;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace GenericMCPServerBackend.Core.Misc
{
    public class HealthCheck : IHealthCheck
    {
        private readonly IGeneralLogger _Logger;
        private readonly IInitializationService<CommandlineParameter> _InitializationService;
        /// <summary>Initializes a new instance of <see cref="HealthCheck"/>.</summary>
        /// <param name="logger">The logger for diagnostic output.</param>
        /// <param name="initializationService">The initialization service used to verify the app is ready.</param>
        public HealthCheck(IServerLog logger, IInitializationService<CommandlineParameter> initializationService)
        {
            this._Logger = logger.Logger;
            this._InitializationService = initializationService;
        }
        /// <inheritdoc />
        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            this._Logger.Log("Start calculating health-status", Microsoft.Extensions.Logging.LogLevel.Debug);
            return await Tools.CheckHealthAsync(this._Logger, () =>
            {
                this._Logger.Log("Calculate health-status...", Microsoft.Extensions.Logging.LogLevel.Debug);
                IList<string> messages = new List<string>();
                HealthStatus result = HealthStatus.Healthy;

                return (result, messages);
            }, context, cancellationToken, this._InitializationService);
        }
    }
}
