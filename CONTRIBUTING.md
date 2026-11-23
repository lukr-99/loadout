# Contributing

## Local verification

Run from the repository root:

```powershell
dotnet format Loadout.slnx --verify-no-changes
dotnet build Loadout.slnx -c Release
dotnet test Loadout.slnx -c Release
```

`dotnet build` treats warnings as errors (see `Directory.Build.props`), so a clean build means zero
warnings. The Windows platform tests (`Loadout.Windows.Tests`) only assert on Windows; on
other hosts they no-op rather than fail.

## Change shape

- Keep each commit to one coherent behavior or repository change.
- Use Conventional Commits: `type(optional-scope): imperative summary`.
- Keep required tests and documentation in the same commit as the behavior they cover.
- Do not commit generated builds (`bin/`, `obj/`, `TestResults/`), secrets, signing files, local SDK
  paths, or machine identifiers.
- Do not add an AI-attribution trailer to commit messages.

## Conventions

This repository follows the shared blueprint in the owner's `CodePrint` (rules) and `dotnetlib`
(.NET architecture) repositories, and uses `Relay` / `MicForge` as reference implementations. Notably:
one top-level type per file, a portable domain project with platform code behind injected seams, and
data-driven action handlers dispatched through a registry.

## Pull requests

State the outcome, the verification you ran, data/migration impact if any, and rollback or recovery
notes for risky delivery changes.
