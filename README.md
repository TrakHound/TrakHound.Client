# TrakHound Client

A .NET client for calling a [TrakHound](https://trakhound.com) Instance: its Apis, Functions, Workers, Services (Tables, Messages, Message Queues, Key Values, Blobs, Time Series), Volumes, Logs and the Instance itself.

It targets .NET Standard 2.0 and .NET 10, so it runs in .NET Framework 4.7.2+ apps (ex. HMIs, WinForms and WPF tools) as well as current .NET.

It's the same client that TrakHound packages use (`Client` in an Api, App, Function or Worker), so code moves between a package and an app unchanged.

## Install

```bash
dotnet add package TrakHound.Client
```

TrakHound is pre-release, so the package is too.

## Connect

```csharp
using TrakHound.Clients;

var configuration = new TrakHoundHttpClientConfiguration("localhost", 8472);
configuration.ApiKey = "...";   // an API Key from the Instance (Admin > Security > API Keys)

var client = new TrakHoundHttpClient(configuration);
```

`TrakHoundHttpClientConfiguration` takes the hostname, port, base path and whether to use HTTPS. Authenticate with one of:

| Setting | Sent as | Use it for |
| --- | --- | --- |
| `ApiKey` | `X-API-Key` header | Apps and services that call the Instance on their own behalf |
| `BearerToken` | `Authorization: Bearer` header | A token from the Instance's Token Identity Provider |
| `ModuleToken` | `X-TrakHound-Module-Token` header | A process started by a TrakHound package, calling as that package |
| (none) | | A local Instance: resources that no Security Profile covers can be read from the Instance's own machine |

A process started by a package can use `TrakHoundHttpClient.FromModuleEnvironment()`, which reads `TRAKHOUND_URL`, `TRAKHOUND_MODULE_TOKEN` and `TRAKHOUND_ROUTER_ID`.

## Use

```csharp
using TrakHound;   // ReadAllAsync for streams

// Call an Api hosted by the Instance
var items = await client.Api.GetJson<List<Item>>("/my-api/items");

// Run a Function
var run = await client.Functions.Run("my-function", new Dictionary<string, string> { ["machine"] = "M1" });

// Query a Table Service
var parts = await client.Services.Tables.ReadMultiple<Part>("select * from parts");

// Subscribe to Messages
var subscription = await client.Services.Messages.Subscribe("machines/+/status");
if (subscription.Success)
{
    await foreach (var message in subscription.Result.ReadAllAsync())
    {
        Console.WriteLine($"{message.Topic}: {message.GetContentUtf8String()}");
    }
}
```

The client's properties follow the Instance:

| Property | What it does |
| --- | --- |
| `Api` | Calls the Apis hosted by the Instance: `Get`, `Post`, `Put`, `Delete`, `Patch` and the rest, each with String, Bytes and Json forms, and `Stream` / `StreamJson` for WebSocket streams |
| `Functions` | Lists Functions, runs them and follows a run's status and log (through the Functions Api package) |
| `Workers` | Lists Workers, starts and stops them, and reads their status and logs (through the Workers Api package) |
| `Services` | `Tables`, `Messages`, `MessageQueues`, `KeyValues`, `Blobs` and `TimeSeries`, plus the Service `Drivers`, `Routers` and `Configurations` |
| `Volumes` | Lists, downloads, uploads and deletes files in the Instance's Volumes |
| `Logs` | Queries and subscribes to the Instance's logs |
| `Instances` | The Instance's information, health, CPU usage and boots |
| `Apps`, `Modules`, `Buffers`, `Streams`, `Mcp`, `HttpRequests` | Information and metrics |

The models keep the namespaces they have in the TrakHound Framework (ex. `TrakHound.Functions` for `TrakHoundFunctionResponse`, `TrakHound.Services.Tables` for Table Requests).

## Don't reference TrakHound.Common too

`TrakHound.Common`, the library TrakHound packages are built on, contains this same client. A project that references both gets errors about ambiguous types (CS0433). Packages use `TrakHound.Common`. Everything else uses `TrakHound.Client`.

## .NET Framework (.NET Standard 2.0)

The .NET Standard 2.0 build behaves the same, with these differences:

- **WebSocket certificates:** .NET Framework can't accept an untrusted certificate (ex. a self-signed one) for a single WebSocket. Over HTTPS, the Instance's certificate must be trusted by the machine for streams (`Api.Stream`, Message subscriptions and the other `Subscribe` calls) to connect. Plain requests are not affected.
- **Compression:** responses are decompressed with gzip and deflate (no Brotli).
- **Cancellation:** a cancellation token stops a request, but not the read of a response that has already arrived.

## Certificates

`TrakHoundHttpRequests.AllowUntrustedCertificates` is `true` by default, so a client accepts an Instance's self-signed certificate. Set it to `false` when the Instance has a trusted certificate.

## Contributing

Most of this repository is copied from the TrakHound Framework. See [CONTRIBUTING.md](CONTRIBUTING.md) for how that works and how to build and test.

## License

[Apache License, Version 2.0](LICENSE)
