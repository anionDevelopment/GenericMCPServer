using GRYLibrary.Core.APIServer.ConcreteEnvironments;
using System.Collections.Generic;

namespace GenericMCPServerBackend.Core.Misc
{
    /// <summary>
    /// General, environment-independent utilities.
    /// </summary>
    internal static class Utilities
    {
        /// <summary>
        /// Returns the <see cref="GRYEnvironment"/> that matches the compile-time build configuration
        /// (Development, QualityCheck, or Productive).
        /// </summary>
        /// <returns>The active <see cref="GRYEnvironment"/> instance.</returns>
        /// <exception cref="KeyNotFoundException">Thrown when none of the expected preprocessor symbols is defined.</exception>
        internal static GRYEnvironment GetEnvironmentTargetType()
        {
#if Development
            return Development.Instance;
#elif QualityCheck
            return QualityCheck.Instance;
#elif Productive
            return Productive.Instance;
#else
            throw new KeyNotFoundException("Unknown environmenttargettype.");
#endif
        }
    }
}
