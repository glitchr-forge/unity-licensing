using System;
using System.IO;
using UnityEngine;

namespace Glitchr.Licensing
{
    /// <summary>
    /// A Unity package's licence, as Forge Store keeps it for its user: the token a buyer pasted for
    /// a package of a store (a static hub's offline licence, most often), in
    /// <c>Forge Store/licences.json</c> under the user's local application data - never in a
    /// project. A licensed package checks itself with it, in the editor:
    /// <code>
    /// LicensePayload licence = PackageLicence.Check("https://example.github.io/hub", "com.example.tool", PublicKey, out string refusal);
    /// if (licence == null) { /* refusal: no_licence, wrong_registry, wrong_machine, expired... */ }
    /// </code>
    /// The package ships its store's public key, as a game does. Nothing here writes the file:
    /// Forge Store does, after checking the token against the store's pinned key.
    /// </summary>
    public static class PackageLicence
    {
        /// <summary>Why <see cref="Check"/> refused: no licence is kept for that registry and package.</summary>
        public const string NoLicence = "no_licence";

        /// <summary>Forge Store's file: <c>Forge Store/licences.json</c> in the local application data (Unity's, as Forge Store's own).</summary>
        public static string DefaultPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Forge Store", "licences.json");

        /// <summary>The token kept for that package of that registry, or null (no file, no entry, a file that does not read).</summary>
        public static string Token(string registry, string package, string path = null)
        {
            path ??= DefaultPath;
            if (string.IsNullOrEmpty(package) || !File.Exists(path)) return null;
            Kept kept;
            try
            {
                kept = JsonUtility.FromJson<Kept>(File.ReadAllText(path));
            }
            catch (Exception)
            {
                return null;
            }
            foreach (Entry entry in kept?.licences ?? Array.Empty<Entry>())
            {
                if (entry != null && entry.package == package && LicenseToken.SameRegistry(entry.registry, registry) && !string.IsNullOrEmpty(entry.token)) return entry.token;
            }

            return null;
        }

        /// <summary>
        /// The kept licence of that package, checked with <see cref="LicenseToken.CheckPackage"/>:
        /// the payload, or null with the reason (<see cref="NoLicence"/>, or one of
        /// <see cref="LicenseToken"/>'s). The machine is <see cref="SystemInfo.deviceUniqueIdentifier"/>
        /// unless given; a licence that names no machine runs on any.
        /// </summary>
        public static LicensePayload Check(string registry, string package, string publicKey, out string refusal, string path = null, string machineIdentifier = null, DateTimeOffset? now = null)
        {
            string token = Token(registry, package, path);
            if (token == null)
            {
                refusal = NoLicence;

                return null;
            }

            return LicenseToken.CheckPackage(token, publicKey, registry, package, machineIdentifier ?? SystemInfo.deviceUniqueIdentifier, out refusal, now);
        }

#pragma warning disable 0649
        [Serializable] private class Kept { public int version; public Entry[] licences; }
        [Serializable] private class Entry { public string registry, package, token; }
#pragma warning restore 0649
    }
}
