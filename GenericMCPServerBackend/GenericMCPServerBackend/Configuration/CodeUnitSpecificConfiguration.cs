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
        /// The file which declares the tools this server offers (see <see cref="Services.ToolDefinitionsReader"/>), as the whole path of that file. It is
        /// written as such when the configuration-file is generated ("Tools.json" in the tools-folder of the server, see Program.cs), so an administrator
        /// reads here where the declaration is looked for and can move it by changing this one value; a value which is relative is resolved against the
        /// tools-folder, where the declaration and the scripts it names belong. Without a file at that location the server offers no tool at all, which is deliberate: a tool exists because
        /// an administrator declared it and not because this codeunit brings it along.
        /// </summary>
        public AbstractFilePath ToolDefinitionsFile { get; set; }
    }
}
