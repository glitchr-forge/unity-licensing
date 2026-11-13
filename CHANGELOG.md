# Changelog

All notable changes to this package are documented here. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and the package
adheres to [Semantic Versioning](https://semver.org/).

## [1.1.0] - 2026-11-13

### Added

- Tokens of format 2: `LicensePayload.registry` and `package` (the store's
  registry and the Unity package the licence covers, as a Forge signs them
  since its plan item 17.2), `licensee` (an offline licence's holder),
  `Bound`, `AnyMachine`, `Ends`, `Holder`.
- `LicenseToken.Check(token, publicKey, software, machine, registry, package)`
  and its overload with `out refusal`: the token bound to its store - format 2,
  that registry (a final slash and the host's case aside), that package (null
  for a game). A format 1 token is refused (`unbound`): activate again online.
- `LicenseToken.CheckPackage(...)`: a Unity package's licence, bound by the
  package's name, its software not checked. `LicenseToken.SameRegistry()`.
  Refusal codes: `not_genuine`, `wrong_software`, `unbound`,
  `wrong_registry`, `wrong_package`, `wrong_machine`, `expired`.
- `LicenseClient.Registry` and `Package`: set, the client's checks are bound.
- `PackageLicence`: the licence Forge Store keeps for its user
  (`Forge Store/licences.json` in the local application data), checked by the
  licensed package itself in the editor.
- Offline licences of a static hub (signed by its author with
  `omnihub licence sign`): a format 2 token without `machine` runs on any
  machine, one without `valid_until` has no end. A machine named and
  different is still refused; a format 1 token still needs both.

## [1.0.1] - 2026-10-26

### Added

- `LicensePayload.RunsOutWithin(margin)`: whether the token runs out soon -
  time to activate again while online.

## [1.0.0] - 2026-10-26

### Added

- `LicenseClient`: activates this machine against a Glitch Art forge
  (`POST /api/licenses/activate`), frees its place (`POST /api/licenses/release`),
  fetches the forge's public key (`GET /api/licenses/public-key`).
- `LicenseToken`: checks a licence token offline - its Ed25519 signature, its
  software, its machine, its `valid_until`.
- `Ed25519`: signature verification in managed code (RFC 8032), no native library.
