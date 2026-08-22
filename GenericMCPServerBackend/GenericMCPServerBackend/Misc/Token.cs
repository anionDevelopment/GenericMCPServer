using System;
using System.Security.Cryptography;
using System.Text;

namespace GenericMCPServerBackend.Core.Misc
{
    /// <summary>
    /// Creates the tokens of this server and hashes them.
    ///
    /// A token exists in a readable form exactly once, namely in the moment it is created: only its hash is stored, so
    /// whoever gets a file of the configuration into their hands holds no access, and a token which somebody lost can
    /// not be looked up but only replaced.
    /// </summary>
    internal static class Token
    {
        /// <summary>The beginning of a token which a client uses to reach an mcp-server.</summary>
        internal const string PrefixOfAnAccessToken = "gmcp_";

        /// <summary>The beginning of the token with which an administrator reaches the administration of the server.</summary>
        internal const string PrefixOfAnAdminToken = "gmcpadmin_";

        /// <summary>The beginning of a token with which a client administrates its own access-tokens.</summary>
        internal const string PrefixOfAClientAdministrationToken = "gmcpclientadmin_";

        /// <summary>
        /// The amount of characters of the random part of a token. They are hexadecimal, so this states 16 bytes of
        /// randomness - enough that a token can not be guessed and short enough to be handed over by a person.
        /// </summary>
        private const int AmountOfCharacters = 32;

        /// <summary>
        /// Creates a random value: a hexadecimal string of <see cref="AmountOfCharacters"/> characters.
        /// </summary>
        /// <remarks>
        /// Every token of this server comes from here - the one of the administrator, the one with which a client
        /// administrates itself and the one with which a client reaches an mcp-server. They are therefore all equally
        /// hard to guess, and there is one place which decides how a token is generated. The bytes come from the
        /// random-generator of the operating-system, which is the one which is meant to be unpredictable.
        /// </remarks>
        internal static string CreateRandomValue()
        {
            return Convert.ToHexString(RandomNumberGenerator.GetBytes(AmountOfCharacters / 2)).ToLowerInvariant();
        }

        /// <summary>
        /// Creates a new token.
        /// </summary>
        /// <param name="prefix">The beginning of the token. It makes a token recognizable as one and states which kind of token it is - in a log, in a configuration-file of a client or in a scan which searches for secrets which were committed by accident.</param>
        internal static string Create(string prefix)
        {
            return prefix + CreateRandomValue();
        }

        internal static string GetHash(string token)
        {
            return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))).ToLowerInvariant();
        }

        /// <summary>
        /// States whether the given token is the one which belongs to the given hash.
        /// </summary>
        /// <remarks>
        /// The comparison of the hashes runs in a time which does not depend on how many characters of them are equal,
        /// so the answer says nothing about how close a wrong token was to a right one.
        /// </remarks>
        internal static bool Matches(string token, string hash)
        {
            if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(hash))
            {
                return false;
            }
            return CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(GetHash(token)), Encoding.UTF8.GetBytes(hash.ToLowerInvariant()));
        }
    }
}
