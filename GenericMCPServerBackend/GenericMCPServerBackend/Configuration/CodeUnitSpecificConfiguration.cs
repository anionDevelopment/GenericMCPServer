using GRYLibrary.Core.APIServer.CommonRoutes;
using GRYLibrary.Core.APIServer.MaintenanceRoutes;
using GRYLibrary.Core.APIServer.Mid.M05DLog;
using GRYLibrary.Core.APIServer.MidT.Exception;
using GRYLibrary.Core.APIServer.MidT.RLog;
using GRYLibrary.Core.Misc.FilePath;

namespace GenericMCPServerBackend.Core.Configuration
{
    /// <summary>
    /// Application-specific configuration of this codeunit. It contains no persistence-configuration for a database,
    /// because this codeunit holds no state of that kind; it is itself part of the persisted configuration-file of the
    /// server (see Program.cs), so every property here is what an administrator can change by editing that file.
    /// </summary>
    public class CodeUnitSpecificConfiguration : ISupportRequestLoggingMiddleware, ISupportExceptionManagerMiddleware
    {
        public ICommonRoutesInformation CommonRoutesInformation { get; set; }
        public IMaintenanceRoutesInformation MaintenanceRoutesInformation { get; set; }
        public IDRequestLoggingConfiguration ConfigurationForDLoggingMiddleware { get; set; }
        public IRequestLoggingConfiguration ConfigurationForLoggingMiddleware { get { return this.ConfigurationForDLoggingMiddleware; } }
        public IExceptionManagerConfiguration ConfigurationForExceptionManagerMiddleware { get; set; }

        /// <summary>
        /// The file which declares the tools this server offers (see <see cref="Services.ToolDefinitionsReader"/>), relative to the tools-folder of the
        /// server unless given as an absolute path. Its default ("./Tools.json", see Program.cs) is only written into the configuration-file the
        /// first time it is generated; an administrator who wants the tools declared somewhere else changes this value there afterwards. Without a file at
        /// the resolved location the server offers no tool at all, which is deliberate: a tool exists because an administrator declared it and not because
        /// this codeunit brings it along.
        /// </summary>
        public AbstractFilePath ToolDefinitionsFile { get; set; }
    }
}
