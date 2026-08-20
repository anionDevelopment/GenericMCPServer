using CommandLine;

namespace GenericMCPServerBackend.Core.Configuration
{
    /// <summary>
    /// The parameters of the call which creates an access-token for a client.
    ///
    /// It is a call of its own and not a route of the server, because it is the way an administrator creates the first
    /// access at all: a route would need an access to be reachable.
    /// </summary>
    [Verb(CreateAccessTokenParameter.VerbName, HelpText = "Creates an access-token for a client and stores it.")]
    public class CreateAccessTokenParameter
    {
        public const string VerbName = "CreateAccessToken";

        /// <summary>The client the token belongs to. A client can have more than one valid token at the same time.</summary>
        [Option(nameof(Client), Required = true, HelpText = "The name of the client the token belongs to.")]
        public string Client { get; set; }

        /// <summary>The amount of days the token is valid. Every token has an end; there is no endless one.</summary>
        [Option(nameof(ValidForDays), Required = false, Default = 365, HelpText = "The amount of days the token is valid.")]
        public int ValidForDays { get; set; }

        /// <summary>
        /// The file which holds the tokens. Without it the file of the configuration-folder of the server is used,
        /// which is the one the server reads.
        /// </summary>
        [Option(nameof(AccessTokensFile), Required = false, HelpText = "The file which holds the tokens. Defaults to the one in the configuration-folder of the server.")]
        public string? AccessTokensFile { get; set; }
    }
}
