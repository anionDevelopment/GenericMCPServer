using System;

namespace GenericMCPServerBackend.Core.Model
{
    /// <summary>
    /// One access-token of one client, the way that client sees it: the hash and never the token itself, and whether
    /// it is still valid at the current point in time.
    /// </summary>
    public sealed class AccessTokenOverview
    {
        public AccessTokenOverview(string hash, DateTime validUntil, bool isValid)
        {
            this.Hash = hash;
            this.ValidUntil = validUntil;
            this.IsValid = isValid;
        }

        public string Hash { get; }

        public DateTime ValidUntil { get; }

        public bool IsValid { get; }
    }
}
