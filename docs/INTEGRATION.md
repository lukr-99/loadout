# Loadout integration contract

> Implemented in Milestone 5. Both transports (protocol URI and CLI), the loopback API path, and
> the trusted confirmation window exist; see [Implementation](#implementation). The installer
> registers the `loadout://` scheme with Windows.
>
> Loadout was called Startup Profiles before. The old `startupprofiles://` scheme is still registered
> and parsed the same way, so links that other apps already use keep working. Use `loadout://` in
> new code. The old scheme is deprecated and will be dropped in a later release.

Loadout exposes a small **public integration contract** so other applications can
*request* to be added to a startup profile. This is aimed at CodePrint-convention Windows
apps but is not specific to them.

The core distinction:

> The external application **requests** registration. Loadout **owns** the decision
> and the configuration UI.

An application effectively says *"I support Loadout - would you like to add me?"*
It never silently adds itself.

## Registration flow

1. User installs or first launches an application.
2. The application detects that Loadout is installed.
3. It invokes the Loadout registration contract (protocol or CLI).
4. Loadout opens **its own trusted** registration window.
5. The window shows the requesting application's information.
6. The user chooses where it belongs: the **Base** (which runs before every profile) / Work /
   Dev / School / Games / Chill / Everything / multiple destinations / **Just recognize** /
   **Don't add**.
7. Loadout stores the configuration.
8. The requesting application receives a success / cancelled result if appropriate.

**Just recognize** is the third answer to "where does this belong?": the app is kept in the global
library and nothing is started anywhere, so the user can drag it into a profile whenever they want.
Every registration goes through the library, so an app added to a profile is editable in one place.

## Transport (proposed)

Prefer a Windows-native custom protocol:

```
loadout://register?appId=...&name=...&target=...&icon=...&args=...&publisher=...&suggestedProfile=...
```

and/or a small CLI:

```
Loadout.exe register --app-id ... --name ... --target ... [--icon ...] [--args ...] \
                             [--publisher ...] [--suggested-profile Dev] [--supports-minimized]
```

The exact transport can change; the conceptual contract below should stay stable.

## Registration metadata

| Field | Meaning |
| --- | --- |
| Application ID | stable unique id for the requesting app |
| Display name | shown in the confirmation UI |
| Executable path / launch target | what a launch action would run |
| Icon | shown in the confirmation UI |
| Default launch arguments | optional args |
| Publisher | shown for trust |
| Suggested profile / category | a *hint* only; the user decides |
| Supports minimized startup | capability flag |
| Optional integration capabilities | see "Future contract expansion" |

## Security / UX rules

External applications must **not** be able to:

- add themselves silently,
- modify existing profiles without consent,
- remove other applications,
- automatically assign themselves to `Everything` or to the base,
- execute arbitrary Loadout configuration changes.

Every registration results in a **Loadout-owned confirmation UI**. The requesting
app supplies metadata and a request - not configuration authority. A suggested profile is a
hint; the user always chooses, and neither `Everything` nor the base is ever auto-selected.

## CodePrint convention

When designing a CodePrint Windows application, consider Loadout integration:

- If Loadout is installed, the app may expose **Add to Loadout**, e.g.
  under **Settings -> Startup -> Add to Loadout**, or offered once during setup.
- The app must work completely normally when Loadout is **not** installed.
- Integration is therefore an **optional capability, never a hard dependency**.

## Implementation

The contract is realised in two layers:

- **`Loadout.Core/Integration`** (portable): `RegistrationRequest` is the metadata an app
  supplies; `RegistrationTargets` is where the user chose to put it (profiles, the base, or neither);
  `RegistrationRequestParser` parses both transports through one shared field mapping and validation;
  `ProfileRegistrar` applies an approved request by keeping the app in the global library and appending
  an action linked to that item to each chosen destination (idempotent per target, never creating
  profiles or touching unlisted ones). A profile that includes the base is skipped when the base
  already starts the app, so it is never launched twice.
- **`Loadout.App/Integration`** (host): the entry points and the trusted window.

Three ways to reach it:

1. **Protocol** - `Loadout.exe "loadout://register?appId=...&name=...&target=..."`.
   The installer registers the scheme with Windows (`startupprofiles://` too, as a deprecated alias).
2. **CLI** - `Loadout.exe register --app-id ... --name ... --target ... [--args ...]
   [--publisher ...] [--suggested-profile Dev] [--supports-minimized]`. A value that itself starts
   with `--` (launch arguments) can use the `--args=--flag` form.
3. **Loopback API** - `POST /api/register` for local agents (see [API.md](API.md)).

The protocol and CLI forms open a **Loadout-owned** confirmation window: it shows the
requesting app's icon, name, publisher, and target, and a scrolling checklist of destinations - the
base first, then every profile, each with what it already starts. A `suggestedProfile` only pre-ticks
that one profile as a hint - never `Everything`, never the base - and the user must click **Add**, or
**Just recognize** to keep it in the library only. Because a running instance caches profiles in
memory, a one-shot `register` process routes its write through the running instance's loopback API
when one is live (keeping a single writer of `profiles.json`), and writes directly only when no
instance is running.

## Future contract expansion

The same contract could later let an app declare richer capabilities than a single launch,
and let the user pick which capability belongs to a profile:

```
MyApp
  Default          normal startup
  Open Project     specific args / deep link
  Background Agent  background-only mode
  Start Minimized  minimized startup
```

Possible declared capabilities: normal startup action, minimized startup action, specific
startup arguments, deep links, available modes, background-only mode, dependencies, and
before/after-launch hooks.
