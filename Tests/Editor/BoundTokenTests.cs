using System;
using System.IO;
using NUnit.Framework;

namespace Glitchr.Licensing.Tests
{
    /// <summary>
    /// Format 2 tokens: bound to their store's registry and package (a Forge's, built here with a
    /// test key in the forge's exact shape), and offline licences of a static hub (omnihub licence
    /// sign's own output, made with a throwaway key whose private half was never kept): no machine
    /// runs on any machine, no valid_until runs without an end.
    /// </summary>
    public class BoundTokenTests
    {
        private const string Registry = "https://forge.example.com/upm";
        private static readonly byte[] ForgeSeed = TestSigner.Seed("forge");
        private static readonly string ForgeKey = TestSigner.PublicKeyBase64(ForgeSeed);
        private static readonly DateTimeOffset Before = DateTimeOffset.FromUnixTimeSeconds(1792972900);

        // omnihub licence sign, PHP 8.5 in composer:2 (Docker, no network), a throwaway key:
        // any: {"v":2,"registry":"https://example.github.io/hub","package":"com.example.tool","licensee":"Ada Example <ada@example.org>","issued_at":1792972800}
        // bound: the same for "ada@example.org", machine = SHA-256("machine-0001"), valid_until 1795564800, updates_until 1824508800.
        private const string HubKey = "wI9lKrPyiFJOD1O7ZjUnVYL4XlfqM5idJ9Di3xjngvA=";
        private const string HubRegistry = "https://example.github.io/hub";
        private const string AnyMachine = "eyJ2IjoyLCJyZWdpc3RyeSI6Imh0dHBzOi8vZXhhbXBsZS5naXRodWIuaW8vaHViIiwicGFja2FnZSI6ImNvbS5leGFtcGxlLnRvb2wiLCJsaWNlbnNlZSI6IkFkYSBFeGFtcGxlIDxhZGFAZXhhbXBsZS5vcmc-IiwiaXNzdWVkX2F0IjoxNzkyOTcyODAwfQ.dvqH85eBu4AAuPaAddcP5z0R6EO8dMKcmls79DJ9MOJaejFcrX2euD0nmc5PtgMfaj-lxBz6GStsLH-X3IAQAQ";
        private const string OneMachine = "eyJ2IjoyLCJyZWdpc3RyeSI6Imh0dHBzOi8vZXhhbXBsZS5naXRodWIuaW8vaHViIiwicGFja2FnZSI6ImNvbS5leGFtcGxlLnRvb2wiLCJsaWNlbnNlZSI6ImFkYUBleGFtcGxlLm9yZyIsIm1hY2hpbmUiOiIzMTdmMDZmNzdiZjcyZTAyY2I2ODJjMzY1ZmQ1ZWIwNDAwYzY0Nzk1MDY1MjhkNzQ1ZjVlYzNmMjNiMDRjYWExIiwiaXNzdWVkX2F0IjoxNzkyOTcyODAwLCJ2YWxpZF91bnRpbCI6MTc5NTU2NDgwMCwidXBkYXRlc191bnRpbCI6MTgyNDUwODgwMH0.BsY86NMALtxcbjWAPZToGg0LecvXF91KrAvjdTHYl1HfE6DQKndQWjJ7U8g6BXQxcjtAyCmrWSvDfil2tw_vCQ";

        /// <summary>A token in omnibase/forge's LicenseToken::issue() shape (same fields, same order, nulls kept).</summary>
        private static string ForgeToken(string registry = Registry, string package = "com.example.tool", string machine = "machine-0001", int v = 2, string software = "example-tool")
        {
            string bound = v >= 2 ? "\"registry\":" + Text(registry) + ",\"package\":" + Text(package) + "," : string.Empty;
            string json = "{\"v\":" + v + ",\"license\":42,\"software\":\"" + software + "\"," + bound + "\"email\":\"ada@example.org\",\"machine\":\""
                + LicenseToken.MachineHash(machine) + "\",\"issued_at\":1792972800,\"valid_until\":1795564800,\"license_expires\":null,\"updates_until\":null}";

            return TestSigner.Token(ForgeSeed, json);
        }

        private static string Text(string value) => value == null ? "null" : "\"" + value + "\"";

        [Test]
        public void TheTestSignerIsRfc8032s()
        {
            byte[] seed = Hex("9d61b19deffd5a60ba844af492ec2cc44449c5697b326919703bac031cae7f60");
            CollectionAssert.AreEqual(Hex("d75a980182b10ab7d54bfed3c964073a0ee172f3daa62325af021a68f707511a"), TestSigner.PublicKey(seed));
            CollectionAssert.AreEqual(Hex("e5564300c360ac729086e2cc806e828a84877f1eb8e5d974d873e065224901555fb8821590a33bacc61e39701cf9b46bd25bf5f0595bbe24655141438e7a100b"), TestSigner.Sign(seed, new byte[0]));
        }

        [Test]
        public void AForgeTokenOfFormat2IsReadAndBoundToItsStore()
        {
            string token = ForgeToken();
            LicensePayload payload = LicenseToken.Verify(token, ForgeKey);
            Assert.AreEqual(2, payload.v);
            Assert.AreEqual(Registry, payload.registry);
            Assert.AreEqual("com.example.tool", payload.package);
            Assert.IsTrue(payload.Bound);
            Assert.IsFalse(payload.AnyMachine, "a Forge's token names its machine");
            Assert.IsTrue(payload.Ends);
            Assert.AreEqual("ada@example.org", payload.Holder);

            Assert.IsNotNull(LicenseToken.Check(token, ForgeKey, "example-tool", "machine-0001", Before), "the unbound check reads a format 2 token too");
            Assert.IsNotNull(LicenseToken.Check(token, ForgeKey, "example-tool", "machine-0001", Registry, "com.example.tool", Before));
            Assert.IsNotNull(LicenseToken.Check(token, ForgeKey, "example-tool", "machine-0001", "HTTPS://Forge.Example.com/upm/", "com.example.tool", Before), "a final slash and the host's case aside");

            Expect(LicenseToken.WrongRegistry, token, "https://other.example.com/upm", "com.example.tool");
            Expect(LicenseToken.WrongRegistry, token, "https://forge.example.com/UPM", "com.example.tool");
            Expect(LicenseToken.WrongRegistry, token, null, "com.example.tool");
            Expect(LicenseToken.WrongPackage, token, Registry, "com.example.other");
            Expect(LicenseToken.WrongPackage, token, Registry, null);
            Expect(LicenseToken.WrongSoftware, token, Registry, "com.example.tool", software: "another-tool");
            Expect(LicenseToken.WrongMachine, token, Registry, "com.example.tool", machine: "machine-0002");
            Expect(LicenseToken.Expired, token, Registry, "com.example.tool", now: DateTimeOffset.FromUnixTimeSeconds(1795564800));
            Expect(LicenseToken.NotGenuine, token, Registry, "com.example.tool", key: TestSigner.PublicKeyBase64(TestSigner.Seed("other store")));
            Expect(null, token, Registry, "com.example.tool");
        }

        [Test]
        public void AGamesTokenNamesNoPackage()
        {
            string token = ForgeToken(package: null, software: "example-game");
            LicensePayload payload = LicenseToken.Verify(token, ForgeKey);
            Assert.IsTrue(string.IsNullOrEmpty(payload.package), "null: not a package");
            Assert.IsNotNull(LicenseToken.Check(token, ForgeKey, "example-game", "machine-0001", Registry, null, Before));
            Assert.IsNull(LicenseToken.Check(token, ForgeKey, "example-game", "machine-0001", Registry, "com.example.tool", Before), "a game's token opens no package");

            var client = new LicenseClient("https://forge.example.com", "example-game", ForgeKey) { MachineIdentifier = () => "machine-0001", Registry = Registry };
            Assert.IsNull(client.Check(ForgeToken(registry: "https://other.example.com/upm", package: null, software: "example-game")), "another store's token");
        }

        [Test]
        public void AFormat1TokenCannotBeBound()
        {
            string token = ForgeToken(v: 1);
            Assert.IsNotNull(LicenseToken.Check(token, ForgeKey, "example-tool", "machine-0001", Before), "still runs where nothing binds it");
            Expect(LicenseToken.Unbound, token, Registry, "com.example.tool");

            // A format 1 token without a machine or an end is still refused: only format 2 may leave them out.
            string bare = TestSigner.Token(ForgeSeed, "{\"v\":1,\"software\":\"example-tool\",\"issued_at\":1792972800}");
            Assert.IsNull(LicenseToken.Check(bare, ForgeKey, "example-tool", "machine-0001", Before));
        }

        [Test]
        public void AnOfflineLicenceRunsOnAnyMachineWithoutAnEnd()
        {
            LicensePayload payload = LicenseToken.Verify(AnyMachine, HubKey);
            Assert.IsNotNull(payload, "omnihub's signature, verified by Forge Seal");
            Assert.AreEqual("Ada Example <ada@example.org>", payload.Holder);
            Assert.IsTrue(payload.AnyMachine);
            Assert.IsFalse(payload.Ends);
            Assert.IsFalse(payload.RunsOutWithin(TimeSpan.FromDays(7)));

            DateTimeOffset far = DateTimeOffset.FromUnixTimeSeconds(4102444800); // 2100
            Assert.IsNotNull(LicenseToken.CheckPackage(AnyMachine, HubKey, HubRegistry, "com.example.tool", "machine-0001", out string refusal, far), refusal);
            Assert.IsNotNull(LicenseToken.CheckPackage(AnyMachine, HubKey, HubRegistry + "/", "com.example.tool", "any other machine", out refusal, far), refusal);
            Assert.IsNull(LicenseToken.CheckPackage(AnyMachine, HubKey, "https://another.github.io/hub", "com.example.tool", "machine-0001", out refusal));
            Assert.AreEqual(LicenseToken.WrongRegistry, refusal);
            Assert.IsNull(LicenseToken.CheckPackage(AnyMachine, HubKey, HubRegistry, "com.example.other", "machine-0001", out refusal));
            Assert.AreEqual(LicenseToken.WrongPackage, refusal);
            Assert.IsNull(LicenseToken.CheckPackage(AnyMachine, ForgeKey, HubRegistry, "com.example.tool", "machine-0001", out refusal), "another store's key");
            Assert.AreEqual(LicenseToken.NotGenuine, refusal);
        }

        [Test]
        public void AnOfflineLicenceThatNamesAMachineIsThatMachinesOnly()
        {
            Assert.IsNotNull(LicenseToken.CheckPackage(OneMachine, HubKey, HubRegistry, "com.example.tool", "machine-0001", out string refusal, Before), refusal);
            Assert.IsNull(LicenseToken.CheckPackage(OneMachine, HubKey, HubRegistry, "com.example.tool", "machine-0002", out refusal, Before), "present and different: refused");
            Assert.AreEqual(LicenseToken.WrongMachine, refusal);
            Assert.IsNull(LicenseToken.CheckPackage(OneMachine, HubKey, HubRegistry, "com.example.tool", "machine-0001", out refusal, DateTimeOffset.FromUnixTimeSeconds(1795564800)));
            Assert.AreEqual(LicenseToken.Expired, refusal);
            Assert.IsTrue(LicenseToken.Verify(OneMachine, HubKey).Covers(DateTimeOffset.FromUnixTimeSeconds(1824508800)));
            Assert.IsFalse(LicenseToken.Verify(OneMachine, HubKey).Covers(DateTimeOffset.FromUnixTimeSeconds(1824508801)));

            // An empty machine is not an absent one in format 1, and a machine given is always compared.
            string other = TestSigner.Token(ForgeSeed, "{\"v\":2,\"registry\":\"" + Registry + "\",\"package\":\"com.example.tool\",\"machine\":\"" + LicenseToken.MachineHash("machine-0002") + "\"}");
            Assert.IsNull(LicenseToken.CheckPackage(other, ForgeKey, Registry, "com.example.tool", "machine-0001", out refusal));
            Assert.AreEqual(LicenseToken.WrongMachine, refusal);
        }

        [Test]
        public void APackageReadsTheLicenceForgeStoreKeeps()
        {
            string folder = Path.Combine(Path.GetTempPath(), "forge-seal-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            try
            {
                string path = Path.Combine(folder, "licences.json");
                Assert.IsNull(PackageLicence.Check(HubRegistry, "com.example.tool", HubKey, out string refusal, path, "machine-0001"));
                Assert.AreEqual(PackageLicence.NoLicence, refusal, "no file yet");

                // Forge Store's file, with fields Forge Seal does not read.
                File.WriteAllText(path, "{\n  \"version\": 1,\n  \"licences\": [\n    {\"registry\": \"https://example.github.io/hub/\", \"package\": \"com.example.tool\", \"store\": \"manifest:https://example.github.io/hub/-/hub.json\", \"token\": \"" + AnyMachine + "\", \"addedAt\": \"2026-11-13T00:00:00Z\", \"later\": {\"x\": 1}},\n    {\"registry\": \"https://forge.example.com/upm\", \"package\": \"com.example.tool\", \"token\": \"" + ForgeToken() + "\"}\n  ],\n  \"unknown\": true\n}\n");
                LicensePayload licence = PackageLicence.Check(HubRegistry, "com.example.tool", HubKey, out refusal, path, "machine-0001");
                Assert.IsNotNull(licence, refusal);
                Assert.AreEqual("Ada Example <ada@example.org>", licence.Holder);
                Assert.AreEqual(ForgeToken(), PackageLicence.Token(Registry, "com.example.tool", path), "each store's own licence");
                Assert.IsNull(PackageLicence.Check(HubRegistry, "com.example.other", HubKey, out refusal, path, "machine-0001"));
                Assert.AreEqual(PackageLicence.NoLicence, refusal);

                File.WriteAllText(path, "{ not json");
                Assert.IsNull(PackageLicence.Token(HubRegistry, "com.example.tool", path), "a file that does not read gives nothing");
            }
            finally
            {
                Directory.Delete(folder, true);
            }
            StringAssert.EndsWith(Path.Combine("Forge Store", "licences.json"), PackageLicence.DefaultPath);
        }

        private static void Expect(string refusal, string token, string registry, string package, string software = "example-tool", string machine = "machine-0001", DateTimeOffset? now = null, string key = null)
        {
            LicensePayload payload = LicenseToken.Check(token, key ?? ForgeKey, software, machine, registry, package, out string why, now ?? Before);
            Assert.AreEqual(refusal, why, registry + " " + package);
            Assert.AreEqual(refusal == null, payload != null);
            Assert.AreEqual(refusal == null, LicenseToken.Check(token, key ?? ForgeKey, software, machine, registry, package, now ?? Before) != null);
        }

        private static byte[] Hex(string hex)
        {
            var bytes = new byte[hex.Length / 2];
            for (int i = 0; i < bytes.Length; i++) bytes[i] = Convert.ToByte(hex.Substring(2 * i, 2), 16);

            return bytes;
        }
    }
}
