using System;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace Glitchr.Licensing
{
    /// <summary>
    /// A licence token, checked offline:
    /// <c>base64url(payload JSON) "." base64url(Ed25519 signature of the payload JSON)</c>.
    /// A game runs when the signature is its store's, the token is for this software and this
    /// machine, and its <c>valid_until</c> has not passed; bound to its store, the token also names
    /// that store's registry and the package it covers (format 2).
    /// </summary>
    public static class LicenseToken
    {
        /// <summary>The payload, when the signature verifies with <paramref name="publicKey"/> (base64); else null.</summary>
        public static LicensePayload Verify(string token, string publicKey)
        {
            if (string.IsNullOrEmpty(token) || string.IsNullOrEmpty(publicKey))
            {
                return null;
            }
            string[] parts = token.Trim().Split('.');
            if (parts.Length != 2)
            {
                return null;
            }

            byte[] json, signature, key;
            try
            {
                json = Base64Url.Decode(parts[0]);
                signature = Base64Url.Decode(parts[1]);
                key = Convert.FromBase64String(publicKey.Trim());
            }
            catch (FormatException)
            {
                return null;
            }
            if (!Ed25519.Verify(signature, json, key))
            {
                return null;
            }

            try
            {
                return JsonUtility.FromJson<LicensePayload>(Encoding.UTF8.GetString(json));
            }
            catch (ArgumentException)
            {
                return null;
            }
        }

        /// <summary>Why a check refused a token: its signature is not the store's key's (or it is no token).</summary>
        public const string NotGenuine = "not_genuine";
        /// <summary>Why a check refused a token: it is another software's.</summary>
        public const string WrongSoftware = "wrong_software";
        /// <summary>Why a check refused a token: format 1, it names no store - activate again, online (a Forge then signs a format 2 token).</summary>
        public const string Unbound = "unbound";
        /// <summary>Why a check refused a token: another store's (its registry is not the one expected).</summary>
        public const string WrongRegistry = "wrong_registry";
        /// <summary>Why a check refused a token: another package's (or a package's, where a game's is expected).</summary>
        public const string WrongPackage = "wrong_package";
        /// <summary>Why a check refused a token: another machine's.</summary>
        public const string WrongMachine = "wrong_machine";
        /// <summary>Why a check refused a token: its valid_until has passed - activate again, online, or ask for a new licence.</summary>
        public const string Expired = "expired";

        /// <summary>
        /// Whether that token lets this software run on this machine now: genuine, its own
        /// software, its own machine, still valid. The payload when it does; else null. A format 2
        /// token that names no machine runs on any machine, and one without <c>valid_until</c>
        /// has no end (an offline licence); a format 1 token always names both. The token's store
        /// is not checked: see the overload with a registry.
        /// </summary>
        public static LicensePayload Check(string token, string publicKey, string software, string machineIdentifier, DateTimeOffset? now = null)
        {
            return Evaluate(token, publicKey, software, machineIdentifier, false, null, null, now, out _);
        }

        /// <summary>
        /// As <see cref="Check(string, string, string, string, DateTimeOffset?)"/>, and bound to its
        /// store: a format 2 token whose <c>registry</c> is <paramref name="registry"/> (a final
        /// slash and the host's case aside) and whose <c>package</c> is <paramref name="package"/>
        /// (null for a game or an application). A format 1 token is refused: it names no store.
        /// </summary>
        public static LicensePayload Check(string token, string publicKey, string software, string machineIdentifier, string registry, string package, DateTimeOffset? now = null)
        {
            return Evaluate(token, publicKey, software, machineIdentifier, true, registry, package, now, out _);
        }

        /// <summary>The bound check, with why it refused (<see cref="NotGenuine"/>, <see cref="Unbound"/>, <see cref="WrongRegistry"/>...; null when it passed).</summary>
        public static LicensePayload Check(string token, string publicKey, string software, string machineIdentifier, string registry, string package, out string refusal, DateTimeOffset? now = null)
        {
            return Evaluate(token, publicKey, software, machineIdentifier, true, registry, package, now, out refusal);
        }

        /// <summary>
        /// A Unity package's licence, checked by the package itself (in the editor): genuine, bound
        /// to <paramref name="registry"/> and <paramref name="package"/>, this machine's when it
        /// names one, still valid when it has an end. Its <c>software</c> is not checked: the
        /// package's name binds it. See <see cref="PackageLicence"/> for the token Forge Store keeps.
        /// </summary>
        public static LicensePayload CheckPackage(string token, string publicKey, string registry, string package, string machineIdentifier, out string refusal, DateTimeOffset? now = null)
        {
            if (string.IsNullOrEmpty(package)) throw new ArgumentException("The package's name is needed.", nameof(package));

            return Evaluate(token, publicKey, null, machineIdentifier, true, registry, package, now, out refusal);
        }

        /// <summary>
        /// Two registry addresses of the same store: equal, a final slash, the scheme's and the
        /// host's case and a default port aside (the path keeps its case).
        /// </summary>
        public static bool SameRegistry(string a, string b)
        {
            if (string.IsNullOrWhiteSpace(a) || string.IsNullOrWhiteSpace(b)) return false;
            a = a.Trim();
            b = b.Trim();
            if (Uri.TryCreate(a, UriKind.Absolute, out Uri x) && Uri.TryCreate(b, UriKind.Absolute, out Uri y))
            {
                return string.Equals(x.Scheme, y.Scheme, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(x.Host, y.Host, StringComparison.OrdinalIgnoreCase)
                    && x.Port == y.Port
                    && x.AbsolutePath.TrimEnd('/') == y.AbsolutePath.TrimEnd('/')
                    && x.Query == y.Query;
            }

            return a.TrimEnd('/') == b.TrimEnd('/');
        }

        private static LicensePayload Evaluate(string token, string publicKey, string software, string machineIdentifier, bool bound, string registry, string package, DateTimeOffset? now, out string refusal)
        {
            LicensePayload payload = Verify(token, publicKey);
            refusal = payload == null ? NotGenuine
                : software != null && payload.software != software ? WrongSoftware
                : bound && payload.v < 2 ? Unbound
                : bound && !SameRegistry(payload.registry, registry) ? WrongRegistry
                : bound && (payload.package ?? string.Empty) != (package ?? string.Empty) ? WrongPackage
                : !payload.AnyMachine && payload.machine != MachineHash(machineIdentifier) ? WrongMachine
                : payload.Ends && (now ?? DateTimeOffset.UtcNow).ToUnixTimeSeconds() >= payload.valid_until ? Expired
                : null;

            return refusal == null ? payload : null;
        }

        /// <summary>The machine as the forge stores it: SHA-256 (hex) of its trimmed identifier.</summary>
        public static string MachineHash(string identifier)
        {
            using (SHA256 sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes((identifier ?? string.Empty).Trim()));
                StringBuilder hex = new StringBuilder(64);
                foreach (byte b in hash)
                {
                    hex.Append(b.ToString("x2"));
                }

                return hex.ToString();
            }
        }
    }

    /// <summary>Base64 with the URL alphabet and no padding, as the forge writes its tokens.</summary>
    public static class Base64Url
    {
        public static byte[] Decode(string text)
        {
            string base64 = text.Replace('-', '+').Replace('_', '/');
            switch (base64.Length % 4)
            {
                case 2: base64 += "=="; break;
                case 3: base64 += "="; break;
                case 1: throw new FormatException("Not base64url.");
            }

            return Convert.FromBase64String(base64);
        }

        public static string Encode(byte[] bytes)
        {
            return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        }
    }
}
