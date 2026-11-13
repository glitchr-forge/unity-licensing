using System;

namespace Glitchr.Licensing
{
    /// <summary>
    /// What a licence token says, as its store signs it: a Forge (omnibase/forge, LicenseToken), or
    /// the author of a static hub, offline (omnihub licence sign). The times are UNIX seconds; 0
    /// where the token has null or nothing (a perpetual licence, every update).
    /// </summary>
    [Serializable]
    public class LicensePayload
    {
        /// <summary>
        /// The token's format: 1, or 2 since the token names its store's registry and its package.
        /// Only a format 2 token can be bound to a store (the Check overloads that take a registry).
        /// </summary>
        public int v;

        /// <summary>The licence's id on the forge (0 for an offline licence).</summary>
        public long license;

        /// <summary>The software's slug: a token for another software is refused.</summary>
        public string software;

        /// <summary>
        /// Format 2: the address of the Unity registry of the store that signed it
        /// (<c>https://&lt;site&gt;/upm</c> for a Forge, the registry a static hub's manifest lists).
        /// A token another store signed never opens this store's packages.
        /// </summary>
        public string registry;

        /// <summary>Format 2: the Unity package it covers (its UPM name); null or empty for a game or an application.</summary>
        public string package;

        /// <summary>The seat's e-mail (a Forge's token).</summary>
        public string email;

        /// <summary>Format 2, an offline licence: who it was sold to, as its seller wrote it (a name, an e-mail).</summary>
        public string licensee;

        /// <summary>
        /// SHA-256 (hex) of the machine identifier it was activated for. A format 2 token without
        /// one (an offline licence: its seller does not know the buyer's machine) runs on any machine.
        /// </summary>
        public string machine;

        public long issued_at;

        /// <summary>Past it, activate again, online. A format 2 token without one runs without an end.</summary>
        public long valid_until;

        /// <summary>When the licence itself ends; 0: perpetual.</summary>
        public long license_expires;

        /// <summary>The releases published until then are covered; 0: every one.</summary>
        public long updates_until;

        public DateTimeOffset ValidUntil => DateTimeOffset.FromUnixTimeSeconds(valid_until);

        /// <summary>Whether the token names its store: format 2, with a registry.</summary>
        public bool Bound => v >= 2 && !string.IsNullOrEmpty(registry);

        /// <summary>A format 2 token that names no machine: it runs on any machine.</summary>
        public bool AnyMachine => v >= 2 && string.IsNullOrEmpty(machine);

        /// <summary>Whether the token has an end (<see cref="valid_until"/>); a format 2 token without one has none.</summary>
        public bool Ends => v < 2 || valid_until != 0;

        /// <summary>Who holds it: the licensee an offline licence names, else the seat's e-mail.</summary>
        public string Holder => !string.IsNullOrEmpty(licensee) ? licensee : email;

        /// <summary>
        /// Whether the token runs out within <paramref name="margin"/> (a week, say): time to
        /// activate again, while online, before the game has to ask. Never for a token without an end.
        /// </summary>
        public bool RunsOutWithin(TimeSpan margin, DateTimeOffset? now = null)
        {
            return Ends && (now ?? DateTimeOffset.UtcNow).Add(margin).ToUnixTimeSeconds() >= valid_until;
        }

        /// <summary>Whether a release published at <paramref name="releasedAt"/> is covered by its updates.</summary>
        public bool Covers(DateTimeOffset releasedAt)
        {
            return updates_until == 0 || releasedAt.ToUnixTimeSeconds() <= updates_until;
        }
    }
}
