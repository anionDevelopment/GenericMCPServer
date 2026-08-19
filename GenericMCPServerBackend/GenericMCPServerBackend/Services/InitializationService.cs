using GenericMCPServerBackend.Core.Configuration;
using GRYLibrary.Core.APIServer.CommonDBTypes;
using GRYLibrary.Core.APIServer.ConcreteEnvironments;
using GRYLibrary.Core.APIServer.Services.Init;
using GRYLibrary.Core.APIServer.Services.Interfaces;
using GRYLibrary.Core.APIServer.Services.Logger;
using GRYLibrary.Core.APIServer.Settings;
using GRYLibrary.Core.APIServer.Utilities;
using GRYLibrary.Core.APIServer.Utilities.InitializationStates;
using GRYLibrary.Core.APIServer.Verbs;
using GRYLibrary.Core.Logging.GeneralPurposeLogger;
using GRYLibrary.Core.Misc;
using System.Collections.Generic;

namespace GenericMCPServerBackend.Core.Services
{
    public class InitializationService : IInitializationService<CommandlineParameter>
    {
        private static readonly object _Lock = new object();
        private readonly IGeneralLogger _GeneralLogger;
        private InitializationState _InitializationState;
        public InitializationService(IServerLog generalLogger)
        {
            this._GeneralLogger = generalLogger.Logger;
        }

        /// <inheritdoc />
        public void Initialize(CommandlineParameter commandlineParameter)
        {
            try
            {
                this.SetInitializationState(new Initializing());
            //do initialization if desired
                this.SetInitializationState(new Initialized());
            }
            catch (System.Exception e)
            {
                this.SetInitializationState(new InitializationFailed());
                this._GeneralLogger.Log("Error while service-initialization.", e);
            }
        }

        /// <inheritdoc />
        public void InitializeBase(RunServer commandlineParameter)
        {

        }

        /// <inheritdoc />
        public InitializationState GetInitializationState()
        {
            lock (_Lock)
            {
                return this._InitializationState;
            }
        }

        /// <inheritdoc />
        public void SetInitializationState(InitializationState initializationState)
        {
            lock (_Lock)
            {
                this._InitializationState = initializationState;
            }
        }
    }
}
