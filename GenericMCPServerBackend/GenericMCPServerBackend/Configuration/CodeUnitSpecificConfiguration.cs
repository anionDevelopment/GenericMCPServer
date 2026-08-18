using GRYLibrary.Core.APIServer.CommonRoutes;
using GRYLibrary.Core.APIServer.MaintenanceRoutes;
using GRYLibrary.Core.APIServer.Mid.M05DLog;
using GRYLibrary.Core.APIServer.MidT.Exception;
using GRYLibrary.Core.APIServer.MidT.RLog;

namespace GenericMCPServerBackend.Core.Configuration
{
    /// <summary>
    /// Application-specific configuration of this codeunit. It contains no persistence-configuration, because this
    /// codeunit holds no state which would have to be stored anywhere.
    /// </summary>
    public class CodeUnitSpecificConfiguration : ISupportRequestLoggingMiddleware, ISupportExceptionManagerMiddleware
    {
        public ICommonRoutesInformation CommonRoutesInformation { get; set; }
        public IMaintenanceRoutesInformation MaintenanceRoutesInformation { get; set; }
        public IDRequestLoggingConfiguration ConfigurationForDLoggingMiddleware { get; set; }
        public IRequestLoggingConfiguration ConfigurationForLoggingMiddleware { get { return this.ConfigurationForDLoggingMiddleware; } }
        public IExceptionManagerConfiguration ConfigurationForExceptionManagerMiddleware { get; set; }
    }
}
