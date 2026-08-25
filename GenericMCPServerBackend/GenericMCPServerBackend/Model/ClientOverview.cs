using System.Collections.Generic;

namespace GenericMCPServerBackend.Core.Model
{
    /// <summary>
    /// One client together with the roles it is in and how many access-tokens it holds, the way an administrator sees
    /// the clients which exist.
    /// </summary>
    public sealed class ClientOverview
    {
        public ClientOverview(string client, IReadOnlyCollection<string> roles, int amountOfAccessTokens)
        {
            this.Client = client;
            this.Roles = roles;
            this.AmountOfAccessTokens = amountOfAccessTokens;
        }

        public string Client { get; }

        public IReadOnlyCollection<string> Roles { get; }

        public int AmountOfAccessTokens { get; }
    }
}
