# Usage

## The client

```csharp
var client = new LicenseClient(siteUrl, softwareSlug, publicKey);
```

- `siteUrl`: the site the licence was sold on, `https://glitchr.io`.
- `softwareSlug`: the software's slug on that site - a key only opens its own
  software.
- `publicKey`: the forge's public key, base64 (`GET /api/licenses/public-key`).
  Ship it in the game rather than fetching it at run time: a key fetched at
  run time could be swapped.

The machine is `SystemInfo.deviceUniqueIdentifier` and its name
`SystemInfo.deviceName`; set `client.MachineIdentifier` and
`client.MachineName` to change them.

## Activating

```csharp
ActivationResult result = await client.ActivateAsync(key);
```

`result.Active` is true when the forge activated the machine and its token
checked out; `result.Token` is then to be stored (the game chooses where:
`PlayerPrefs`, a file under `Application.persistentDataPath`). Activating an
already active machine takes no new place: call it again whenever the token
is about to run out (`result.License.RunsOutWithin(TimeSpan.FromDays(7))`).

Otherwise `result.Error` says why:

| Error | What to do |
|---|---|
| `invalid_key` | Ask for the key again. |
| `wrong_software` | The key is another software's. |
| `license_not_valid` | The licence expired or was revoked: say so. |
| `no_machine_left` | Show `result.Machines`; open `result.ManageUrl` to free one. |
| `not_configured` | The site has no signing key yet. |
| `token_not_genuine` | The token's signature, software or machine is wrong: do not run. |
| `network_error: ...` | No answer: run on the stored token while it is valid. |

## At each start, offline

```csharp
LicensePayload licence = client.Check(storedToken);
```

Null when the token is not the forge's, is another software's or another
machine's, or has passed its `valid_until`. Then activate again, online.

## Bound to the store

Tokens of format 2 name their store's registry and, for a Unity package, the
package (see [token.md](token.md)). Set the client's registry - the hub
manifest's `licensing.registry` - and a token another store signed is refused,
whatever it says:

```csharp
var client = new LicenseClient(siteUrl, softwareSlug, publicKey) { Registry = "https://glitchr.io/upm" };
// a Unity package: client.Package = "com.example.tool"; a game or an application: leave it null.
```

`Check` and `ActivateAsync` then use the bound check; a format 1 token (one
signed before the forge bound them) is refused: activate again, online.

## A package's licence, in the editor

A Unity package sold by a store checks itself in the editor. On a static hub,
its buyer pastes the licence its seller gave them into Forge Store, which
checks it against the store's pinned key, the registry and the package, and
keeps it for that user in `Forge Store/licences.json` (local application
data, never the project). The package reads it back and checks it with the
public key it ships:

```csharp
#if UNITY_EDITOR
LicensePayload licence = PackageLicence.Check("https://owner.github.io/hub", "com.example.tool", PublicKey, out string refusal);
if (licence == null)
{
    // refusal: no_licence (none pasted yet), wrong_registry, wrong_package,
    // wrong_machine, expired, not_genuine, unbound
}
#endif
```

The machine is `SystemInfo.deviceUniqueIdentifier`; a licence that names
none runs on any machine, one without `valid_until` has no end.
`PackageLicence.Token(registry, package)` gives the kept token alone. After
the store changes its key, release the package with the new key (or check
with both).

## A game sold by a static hub

A game has no Forge to activate against: the player pastes the licence into
the game, which keeps it where it likes and checks it at each start:

```csharp
LicensePayload licence = LicenseToken.Check(token, PublicKey, "my-game", SystemInfo.deviceUniqueIdentifier,
    "https://owner.github.io/hub", null, out string refusal);
```

## Freeing the machine

```csharp
bool released = await client.ReleaseAsync(key);
```

Before an uninstall, from a "deactivate" menu: the seat's place goes back to
its holder.

## A development site

A site with a self-signed certificate (a local forge) is refused by
UnityWebRequest. Give the client a handler that accepts that certificate,
for development builds only:

```csharp
client.CertificateHandler = () => new MyPinnedCertificateHandler();
```
