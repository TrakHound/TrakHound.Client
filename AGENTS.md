# TrakHound Client

The .NET client for calling a TrakHound Instance, built for .NET Standard 2.0 (so .NET Framework 4.7.2+ apps can use it) and .NET 10. See [README.md](README.md) for what it does.

## Layout

| Folder | What's there |
| --- | --- |
| `src/TrakHound.Client/Synced` | Copies of files from the TrakHound Framework's `libraries/TrakHound.Common`: the client interfaces and HTTP clients and every model and helper they use. **Never edit them here** |
| `src/TrakHound.Client/Polyfills` | .NET Standard 2.0 only: internal shims for the .NET members the synced files use and .NET Standard 2.0 doesn't have (C# 14 extension blocks for static members, extension methods for instance members) |
| `src/TrakHound.Client/Stubs` | Both targets: internal stand-ins for the few Framework types the synced files touch but a client doesn't need (YamlDotNet's `YamlIgnore`, the Instance's Secrets) |
| `sync` | `sync-files.txt` (which files are copied), `sync.ps1` (copies them) and `SYNCED_FROM` (the Framework commit and each file's hash, written by `sync.ps1`) |
| `tests/TrakHound.Client.Tests` | xUnit tests on `net10.0` and `net48` (Windows): the synced files check, the shims, and the contract tests against a real Instance |

## Syncing

- The TrakHound Framework is the source of truth. A change to a synced file is made there, committed, and then synced: `./sync/sync.ps1 -Framework <Framework checkout>` (it defaults to a `TrakHound.Framework` folder next to this repository). Commit the changed `Synced` files and `SYNCED_FROM` together.
- `SyncedFilesTests` fails if a file in `Synced` doesn't match `SYNCED_FROM`, or if a file there wasn't synced. `./sync/sync.ps1 -Check` runs the same check.
- When a synced file starts using another file from TrakHound.Common, add it to `sync-files.txt`. Don't copy Framework code that depends on modules, packages, the Service Router, Security or Zones: change the Framework so the client file doesn't need it (the factories in `TrakHoundApiInformationFactory` and similar are examples).
- Where .NET Standard 2.0 lacks something, prefer a shim in `Polyfills` (it must behave like the .NET member it replaces, and `ShimTests` checks it on both targets). Use `#if NET` in the Framework file only where a shim can't work (an override, a type the code constructs, `await using`, a constructor).

## Build and test

- `dotnet build src/TrakHound.Client/TrakHound.Client.csproj` builds both targets.
- `dotnet test tests/TrakHound.Client.Tests/TrakHound.Client.Tests.csproj` runs on `net10.0` and, on Windows, `net48` (which uses the .NET Standard 2.0 build).
- `InstanceContractTests` run when `TRAKHOUND_CLIENT_TEST_URL` is set (ex. `http://localhost:8472/`), with `TRAKHOUND_CLIENT_TEST_API_KEY` if needed. They compare the Instance's OpenAPI schemas with the synced models and read its parameterless GET responses with unmapped properties disallowed, so a model that is missing a field fails. Run them against an Instance built from the Framework commit in `SYNCED_FROM` after each sync.

## Conventions

- `LangVersion` is 14 on both targets. Polyfills and Stubs are `internal` and in the `TrakHound` namespace (or the namespace of the type they stand in for), so the synced files see them without new usings and apps never do.
- **Pre-release:** renames don't need compatibility shims.
- **Docs:** update the README when behavior or a difference between the targets changes. Write in plain, direct sentences.
- **Style:** match the surrounding code. Comments explain why, briefly.

## Git

- Commit on a branch; `main` is updated by fast-forwarding it to finished branches. Don't push unless asked.
- Commit messages: a short imperative subject and a body that explains what changed and why. A sync commit says which Framework commit it is from.
