using GRYLibrary.Core.APIServer.ConcreteEnvironments;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using System;
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

        /// <summary>
        /// The http-request which the given services belong to.
        ///
        /// A tool, a resource, a prompt and a completion are run deep inside the SDK, which hands them the services of
        /// the request and nothing else, so this is the only way from one of them to the request it is running in.
        /// </summary>
        /// <param name="services">The services of a request of the protocol.</param>
        /// <returns>The request, or null when there is none (which is the case when the code is not running inside one).</returns>
        internal static HttpContext GetHttpContextOf(IServiceProvider services)
        {
            return services?.GetService<IHttpContextAccessor>()?.HttpContext;
        }
    }
}
