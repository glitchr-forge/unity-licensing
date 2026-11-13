# The token

A store answers an activation (a Forge), or its author signs offline (a
static hub, `omnihub licence sign`), a token the software keeps and checks
offline:

```
token = base64url(payload JSON) "." base64url(Ed25519 signature of the payload JSON)
```

The payload (`LicensePayload`):

| Field | |
|---|---|
| `v` | 2 (1: before `registry` and `package`) |
| `license` | the licence's id on the forge (absent from an offline licence) |
| `software` | the software's slug |
| `registry` | format 2: the address of the store's Unity registry - a Forge's `https://<site>/upm` (its hub manifest's `licensing.registry`), a static hub's registry as its manifest lists it (`sources[0].url`) |
| `package` | format 2: the Unity package it covers (its UPM name); null for a game or an application |
| `email` | the seat's e-mail (a Forge's token) |
| `licensee` | an offline licence: who it was sold to, as the seller wrote it |
| `machine` | SHA-256 (hex) of the machine identifier; a format 2 token without it runs on any machine |
| `issued_at` | UNIX time |
| `valid_until` | UNIX time: past it, activate again, online; a format 2 token without it has no end |
| `license_expires` | UNIX time; 0 (null on the forge): perpetual |
| `updates_until` | UNIX time; 0 (null): every release is covered |

`LicenseToken.Check()` runs the game when, all of them, the signature
verifies with the public key it ships with, `software` is its own slug,
`machine` is the hash of its own machine identifier, and now is before
`valid_until`. For a version gate, `LicensePayload.Covers(releaseDate)`
compares a release's date with `updates_until`.

`valid_until` is the forge's grace period (30 days by default) after the last
activation, never beyond the licence's expiry: a machine offline for longer
comes back online once; a revoked licence or a freed machine stops at the
latest then.

## Bound to its store (format 2)

A token names the store that signed it: `registry`, and `package` for a Unity
package. The overloads that take them refuse a token of another store, even
for a package of the same name, and a format 1 token, which names no store:

```csharp
LicenseToken.Check(token, publicKey, software, machine, registry, package);           // a game: package null
LicenseToken.Check(token, publicKey, software, machine, registry, package, out refusal);
LicenseToken.CheckPackage(token, publicKey, registry, package, machine, out refusal);  // a package: software not checked
```

Registries are compared as addresses: a final slash, the scheme's and the
host's case and a default port aside; the path keeps its case.
`LicenseToken.SameRegistry(a, b)` is that comparison. A refusal is one of
`not_genuine`, `wrong_software`, `unbound` (format 1: activate again online,
the forge then signs a format 2 token), `wrong_registry`, `wrong_package`,
`wrong_machine`, `expired`.

## Offline licences (a static hub)

A static hub (a template on GitHub Pages) has no server: its author signs
licences on their own machine with the private key whose public half the
hub's manifest declares (`licensing.publicKey`), and sells them through a link
of their own. The seller does not know the buyer's machine, so such a licence
usually names none:

```json
{"v": 2, "registry": "https://owner.github.io/hub", "package": "com.example.tool",
 "licensee": "Ada <ada@example.org>", "issued_at": 1792972800}
```

- no `machine`: it runs on any machine - only when absent; a machine named and
  different is refused. Only a format 2 token may leave it out.
- no `valid_until`: it has no end (`LicensePayload.Ends` is false). A seller
  may still set one (a yearly licence), and `updates_until`.
- `software` is optional for a package: `CheckPackage` binds by the package's
  name. A game sold offline has `software` and no `package`.

What it can check: the store (registry), the package, the end, and one machine
when the seller asked the buyer for it. What it cannot: seats and revocation -
a copied token works wherever it is pasted. Light protection, not DRM.

Forge Store keeps the token the buyer pasted (after checking it against the
store's pinned key, its registry and the package) in
`Forge Store/licences.json` under the user's local application data - never
in a project; `PackageLicence` reads it (see [usage.md](usage.md)).

The verification is `Ed25519.Verify(signature, message, publicKey)`, RFC 8032
in managed code: no native plug-in, the same on every platform.
