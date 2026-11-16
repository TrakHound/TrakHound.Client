# Contributing to TrakHound Client

Thanks for helping. Bug reports, fixes and documentation are all welcome.

- **Questions and bugs:** open an [issue](https://github.com/TrakHound/TrakHound.Client/issues/new).
- **Security problems:** don't open an issue. Report them privately (see [SECURITY.md](SECURITY.md)).

Everyone taking part is expected to follow the [Code of Conduct](CODE_OF_CONDUCT.md).

## Where the code comes from

Most of the client (everything under `src/TrakHound.Client/Synced`) is copied from the TrakHound Framework, where the Instance and the modules use the same code. Those files are never edited here: `sync/sync.ps1` replaces them, and a test checks that they still match what was synced. A fix to a synced file is made in the Framework and then synced, so open an issue (or a pull request that describes the change) and we'll make it there.

The code that is only in this repository is what makes the synced files build for .NET Standard 2.0 (`src/TrakHound.Client/Polyfills` and `Stubs`), the tests and the docs. Pull requests for those are welcome here.

## Building and testing

You need the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0). The .NET Framework tests also need Windows.

```bash
dotnet build src/TrakHound.Client/TrakHound.Client.csproj
```

```bash
dotnet test tests/TrakHound.Client.Tests/TrakHound.Client.Tests.csproj
```

The tests in `InstanceContractTests` call a real Instance, to check that the synced models still match what it sends. They run when `TRAKHOUND_CLIENT_TEST_URL` is set (ex. `http://localhost:8472/`), with `TRAKHOUND_CLIENT_TEST_API_KEY` if the Instance needs an API Key. Without it they return without checking anything.

## Pull requests

1. Fork the repository and create a branch from `main`.
2. Keep each pull request to one change.
3. Write commit messages with a short imperative subject and a body that explains what changed and why.
4. Open the pull request against `main`.

## License

By contributing, you agree that your contributions are licensed under the [Apache License, Version 2.0](LICENSE), the same license as the project (section 5 of the license). You keep the copyright to your contributions.
