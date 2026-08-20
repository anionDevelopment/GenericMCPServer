using GRYLibrary.Core.APIServer.Mid.M01APIK;

namespace GenericMCPServerBackend.Core.Configuration
{
    /// <summary>
    /// The configuration of the check of the access-tokens.
    ///
    /// It carries no value of its own: which tokens are valid is decided by the file the tokens are stored in, and
    /// whether a request needs one is decided by its route. The class exists because the middleware of GRYLibrary
    /// which this codeunit builds on expects a configuration.
    /// </summary>
    public sealed class AccessTokenValidatorConfiguration : APIKeyValidatorConfiguration
    {
    }
}
