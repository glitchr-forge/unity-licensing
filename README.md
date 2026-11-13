# Forge Seal

Forge Seal (`io.glitchr.licensing`) lets a game or an application sold on a Forge store
check its licence: it activates the player's machine with their licence key,
keeps the signed token the forge answers with, and checks that token offline
at each start - no server call until it expires.

- Activation, refresh and release against the forge's licence API.
- Offline check of the token: Ed25519 signature (managed code, every platform
  Unity builds for), software, machine, validity - and, for a token of format
  2, its store's registry and its package (a token another store signed is
  refused).
- Offline licences of a static hub (signed by its author, no server): a token
  that names no machine runs on any machine, one without an end has none. A
  licensed package checks the licence Forge Store keeps for its user
  (`PackageLicence.Check`).
- No dependency beyond Unity itself (2021.3 or later).

## Installation

The package is served by the forge's Unity registry. In the project's
`Packages/manifest.json`:

```json
{
  "scopedRegistries": [
    {
      "name": "Glitch Art",
      "url": "https://glitchr.io/upm",
      "scopes": ["io.glitchr"]
    }
  ],
  "dependencies": {
    "io.glitchr.licensing": "1.1.0"
  }
}
```

Then **Window > Package Manager > My Registries** lists it. It can also be
added from its Git URL: `https://github.com/glitchr-forge/unity-licensing.git`.
See [docs/installation.md](docs/installation.md).

## Usage

```csharp
using Glitchr.Licensing;

const string PublicKey = "<the forge's public key, base64>";
var client = new LicenseClient("https://glitchr.io", "my-game", PublicKey);

// At start: a stored token that still runs, offline.
LicensePayload licence = client.Check(storedToken);

// Otherwise, or when it is about to run out: activate, online.
if (licence == null)
{
    ActivationResult result = await client.ActivateAsync(playerKey);
    if (result.Active) { storedToken = result.Token; licence = result.License; }
    else { /* result.Error: invalid_key, no_machine_left (result.Machines, result.ManageUrl)... */ }
}
```

A licensed Unity package, in the editor, with the licence its buyer pasted
into Forge Store:

```csharp
LicensePayload licence = PackageLicence.Check("https://owner.github.io/hub", "com.example.tool", PublicKey, out string refusal);
```

See [docs/usage.md](docs/usage.md) and, for what the token holds,
[docs/token.md](docs/token.md).

## Tests

The editor tests (Window > General > Test Runner > EditMode) check the
Ed25519 verification against RFC 8032's vectors, a token signed as the forge
signs it (format 1, and format 2 bound to its registry and package, built
with a test key), offline licences signed by `omnihub licence sign` (any
machine, one machine, no end), and the licences file Forge Store keeps. Add the package to the project's `testables` to see them:

```json
"testables": ["io.glitchr.licensing"]
```

## Licence

MIT - see [LICENSE.md](LICENSE.md).
