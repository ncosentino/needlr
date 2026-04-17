---
applyTo: "**/*HttpClientOptions.cs"
---

# HttpClient Options Rules (`*HttpClientOptions.cs`)

Use Needlr's `[HttpClientOptions]` source generator to register named `HttpClient` instances. This replaces all hand-written `AddHttpClient(...)` calls in plugins — no plugin code required.

## Shape

- Must be a `public sealed record` — not a class or struct
- Must be decorated with `[HttpClientOptions]` from `NexusLabs.Needlr.Generators`
- Must implement at minimum `INamedHttpClientOptions` (enforced by analyzer `NDLRHTTP001`)
- Use `IStandardHttpClientOptions` as a convenience aggregate for the full standard capability set

## Quick start — full standard configuration

```csharp
using NexusLabs.Needlr.Generators;

[HttpClientOptions]
public sealed record MyServiceHttpClientOptions : IStandardHttpClientOptions
{
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(15);
    public string? UserAgent { get; init; } = "MyApp/1.0";
    public Uri? BaseAddress { get; init; }
    public IReadOnlyDictionary<string, string>? DefaultHeaders { get; init; }
}
```

## Minimal surface — only the capabilities you need

When you only need a subset of configuration, implement specific capability interfaces rather than `IStandardHttpClientOptions`. Needlr emits wiring only for the interfaces you implement — no dead code for capabilities you don't use:

```csharp
// Only timeout — no UserAgent, BaseAddress, or DefaultHeaders wiring emitted
[HttpClientOptions("Upstream:Tavily")]
public sealed record TavilyHttpClientOptions : INamedHttpClientOptions, IHttpClientTimeout
{
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(10);
}
```

## Capability interfaces

| Interface | Property | Emitted wiring |
|-----------|----------|----------------|
| `INamedHttpClientOptions` | (marker) | Required on every `[HttpClientOptions]` type |
| `IHttpClientTimeout` | `TimeSpan Timeout` | `client.Timeout = options.Timeout;` |
| `IHttpClientUserAgent` | `string? UserAgent` | `client.DefaultRequestHeaders.UserAgent.ParseAdd(...)` |
| `IHttpClientBaseAddress` | `Uri? BaseAddress` | `client.BaseAddress = options.BaseAddress;` |
| `IHttpClientDefaultHeaders` | `IReadOnlyDictionary<string, string>? DefaultHeaders` | Adds each header to `client.DefaultRequestHeaders` |
| `IStandardHttpClientOptions` | All four above | Convenience aggregate |

## Client name resolution

The `HttpClient` name (used with `IHttpClientFactory.CreateClient("Name")`) is resolved at compile time in strict precedence order:

1. Attribute `Name` argument: `[HttpClientOptions(Name = "tavily-primary")]`
2. Literal `ClientName` property: `public string ClientName => "tavily-primary";`
3. Type-name suffix stripping (fallback): `TavilyHttpClientOptions` → `"Tavily"`

Pick **one** source and stick with it. If two sources disagree, analyzer `NDLRHTTP002` reports a compile error. If `ClientName` is computed (not a string literal), `NDLRHTTP003` fires — use the attribute `Name` argument instead.

Two types resolving to the same client name is also a compile error (`NDLRHTTP005`).

## Configuration section

The default section is `HttpClients:<ResolvedName>`. Override with the attribute's first constructor argument:

```csharp
// Section: HttpClients:MyService (inferred from type name)
[HttpClientOptions]
public sealed record MyServiceHttpClientOptions : IStandardHttpClientOptions { ... }

// Section: Upstream:Tavily (explicit override)
[HttpClientOptions("Upstream:Tavily")]
public sealed record TavilyHttpClientOptions : INamedHttpClientOptions, IHttpClientTimeout { ... }
```

Nested paths use `:` as the delimiter, consistent with the standard .NET configuration path separator.

## appsettings.json

All fields are optional — record property initializers serve as defaults. Only override what needs to change per environment:

```json
{
  "HttpClients": {
    "MyService": {
      "Timeout": "00:00:30",
      "BaseAddress": "https://api.myservice.com/"
    }
  }
}
```

## Consuming the HttpClient

```csharp
// Inject IHttpClientFactory and create by name
public sealed class MyServiceClient(IHttpClientFactory _factory)
{
    public async Task<string> GetAsync(string path, CancellationToken ct)
    {
        var http = _factory.CreateClient("MyService");
        // ...
    }
}

// Or inject IOptions<T> alongside if you also need config values at runtime
public sealed class MyServiceClient(
    IHttpClientFactory _factory,
    IOptions<MyServiceHttpClientOptions> _options)
{
    // ...
}
```

The record is also registered as `IOptions<T>` alongside the `HttpClient` — inject it if you need access to config values at runtime.

## What NOT to do in plugins

Never manually register an `HttpClient` in a plugin — `[HttpClientOptions]` handles all registration automatically:

```csharp
// ❌ WRONG — unnecessary, creates duplicate wiring
options.Services.AddHttpClient("MyService", client =>
{
    client.Timeout = TimeSpan.FromSeconds(15);
    client.BaseAddress = new Uri("https://api.myservice.com/");
});
```
