---
description: Source-generated named HttpClient registration in Needlr via the [HttpClientOptions] attribute. Reuses the [Options] configuration-binding story, uses marker interfaces for capability composition, and is extensible to resilience and handler chains without breaking existing consumers.
---

# Named HttpClient Registration

The `[HttpClientOptions]` attribute source-generates named `HttpClient` registrations from a typed options record. Consumers define the record, implement a few marker interfaces for the capabilities they want configured, and Needlr emits both the `AddOptions<T>().BindConfiguration(...)` binding and the matching `services.AddHttpClient("Name", (sp, client) => { ... })` call — operators override anything via `appsettings.json` without a rebuild.

## Quick Start

```csharp
using NexusLabs.Needlr.Generators;

[HttpClientOptions]
public sealed record WebFetchHttpClientOptions : IStandardHttpClientOptions
{
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(15);
    public string? UserAgent { get; init; } = "MyApp/1.0";
    public Uri? BaseAddress { get; init; }
    public IReadOnlyDictionary<string, string>? DefaultHeaders { get; init; }
}
```

```json
// appsettings.json — all fields optional, record initializers are the defaults
{
  "HttpClients": {
    "WebFetch": {
      "Timeout": "00:00:30",
      "UserAgent": "MyApp-Prod/1.0"
    }
  }
}
```

```csharp
// Consume via IHttpClientFactory exactly as you would today
public sealed class WebFetchService(IHttpClientFactory factory)
{
    private readonly HttpClient _http = factory.CreateClient("WebFetch");
}
```

That is the whole registration. There is no `IServiceCollectionPlugin` code, no `AddHttpClient(...)` call, and no matching `AddOptions<>().BindConfiguration(...)` call to hand-write. The generator emits both into `TypeRegistry.g.cs` and wires them through the existing options-registration bootstrap path.

!!! warning "Configuration must be passed explicitly"

    The generated code binds options via `AddOptions<T>().BindConfiguration(...)`, which reads
    from the `IConfiguration` registered in DI. If you use the **parameterless**
    `BuildServiceProvider()` overload, it registers an **empty** configuration — your
    `appsettings.json` values (including `BaseAddress`, `Timeout`, etc.) will be silently
    ignored and only the record's default property values will apply.

    **Always pass an `IConfiguration` when using `[Options]` or `[HttpClientOptions]`:**

    ```csharp
    var config = new ConfigurationBuilder()
        .SetBasePath(AppContext.BaseDirectory)
        .AddJsonFile("appsettings.json", optional: true)
        .Build();

    var provider = new Syringe()
        .UsingSourceGen()
        .BuildServiceProvider(config); // ← pass config explicitly
    ```

    Web applications using `ForWebApplication()` or host-based apps using `ForHost()` handle
    this automatically — the host builder loads `appsettings.json` as part of its default
    configuration pipeline.

## Capability Interfaces

Configuration is composed from small marker interfaces. The generator emits wiring for each capability **only if** the type implements the corresponding interface — there is no dead code for capabilities you don't use.

| Interface                      | Emits                                                                  |
|--------------------------------|-------------------------------------------------------------------------|
| `INamedHttpClientOptions`      | Required marker — every `[HttpClientOptions]` type must implement this |
| `IHttpClientTimeout`           | `client.Timeout = options.Timeout;`                                    |
| `IHttpClientUserAgent`         | `client.DefaultRequestHeaders.UserAgent.ParseAdd(options.UserAgent);` (guarded by null/empty check) |
| `IHttpClientBaseAddress`       | `client.BaseAddress = options.BaseAddress;` (guarded by null check)    |
| `IHttpClientDefaultHeaders`    | `foreach (var kvp in options.DefaultHeaders) client.DefaultRequestHeaders.Add(...);` (guarded by null check) |
| `IStandardHttpClientOptions`   | Convenience aggregate that implements all of the above                 |

Most consumers implement `IStandardHttpClientOptions` and fill in the four properties. When you want a minimal surface — e.g. a client that only needs a timeout — implement just the specific interfaces:

```csharp
[HttpClientOptions("Upstream:Tavily")]
public sealed record TavilyHttpClientOptions : INamedHttpClientOptions, IHttpClientTimeout
{
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(10);
}
```

The generated `AddHttpClient` callback for `TavilyHttpClientOptions` contains exactly one line: `client.Timeout = options.Timeout;`. No `UserAgent`, `BaseAddress`, or `DefaultHeaders` wiring is emitted because those interfaces aren't implemented.

## Name Resolution

The HttpClient name is resolved at compile time from three sources, in strict precedence order:

| Precedence | Source                                   | Example                                                             |
|------------|------------------------------------------|---------------------------------------------------------------------|
| 1 (highest)| Attribute `Name` argument                | `[HttpClientOptions(Name = "tavily-primary")]`                      |
| 2          | Literal `ClientName` property body       | `public string ClientName => "tavily-primary";`                     |
| 3 (fallback)| Type-name suffix stripping              | `TavilyHttpClientOptions` → `"Tavily"` (strips `HttpClientOptions`) |

Suffix stripping recognizes `HttpClientOptions`, `HttpClientSettings`, and `HttpClient` in that order. A type named `ExaHttpClient` resolves to `"Exa"`.

When multiple sources are present, they **must agree** — if the attribute `Name` and a literal `ClientName` property disagree, the analyzer reports `NDLRHTTP002` at compile time. Pick one source and stick with it.

If `ClientName` is computed rather than a literal (e.g. `public string ClientName => $"{_prefix}-client";`), the generator cannot resolve it statically and reports `NDLRHTTP003`. Use the attribute `Name` argument for dynamic cases.

## Section Name

By default the configuration section is `"HttpClients:<ResolvedName>"`. You can override with the attribute's constructor argument:

```csharp
// Section: "HttpClients:WebFetch" (inferred from type name)
[HttpClientOptions]
public sealed record WebFetchHttpClientOptions : IStandardHttpClientOptions { /* ... */ }

// Section: "Upstream:Tavily" (explicit)
[HttpClientOptions("Upstream:Tavily")]
public sealed record TavilyHttpClientOptions : INamedHttpClientOptions, IHttpClientTimeout { /* ... */ }

// Section: "HttpClients:tavily-primary" (inferred from attribute Name)
[HttpClientOptions(Name = "tavily-primary")]
public sealed record TavilyPrimaryHttpClientOptions : IStandardHttpClientOptions { /* ... */ }
```

Nested paths with colons (the standard .NET configuration path separator) are supported and passed straight through to `BindConfiguration`.

## Defaults and Overrides

Defaults come from the record's property initializers and apply when the `appsettings` section is absent:

```csharp
[HttpClientOptions]
public sealed record WebFetchHttpClientOptions : IStandardHttpClientOptions
{
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(15);
    public string?  UserAgent { get; init; } = "MyApp/1.0";
    public Uri?     BaseAddress { get; init; }
    public IReadOnlyDictionary<string, string>? DefaultHeaders { get; init; }
}
```

With no matching section in `appsettings.json`, the generated `HttpClient` gets `Timeout = 15s` and `UserAgent = "MyApp/1.0"`. Operators override any subset in config without touching the code:

```json
{
  "HttpClients": {
    "WebFetch": {
      "Timeout": "00:00:30"
    }
  }
}
```

The `UserAgent` default still applies — only `Timeout` is overridden. This is the same behavior the `[Options]` pipeline provides, because under the hood the generator emits `AddOptions<T>().BindConfiguration(...)` exactly the same way.

## Also Accessing the Options Record Directly

The same record is registered as `IOptions<T>` alongside the `HttpClient`, so services that need both runtime access to the typed config and the `HttpClient` itself can inject both:

```csharp
public sealed class WebFetchService(
    IHttpClientFactory factory,
    IOptions<WebFetchHttpClientOptions> options)
{
    private readonly HttpClient _http = factory.CreateClient("WebFetch");
    private readonly WebFetchHttpClientOptions _config = options.Value;
}
```

The raw record type itself is **not** registered as a singleton — follow the idiomatic .NET pattern and take `IOptions<T>`, `IOptionsSnapshot<T>`, or `IOptionsMonitor<T>` instead.

## Migrating from Hand-Written `AddHttpClient`

**Before** — typical hand-written plugin code:

```csharp
options.Services.AddHttpClient("WebFetch", client =>
{
    client.Timeout = TimeSpan.FromSeconds(15);
    client.DefaultRequestHeaders.UserAgent.ParseAdd("MyApp/1.0");
});
options.Services.AddHttpClient("Brave", client =>
{
    client.Timeout = TimeSpan.FromSeconds(15);
    client.BaseAddress = new Uri("https://search.brave.com/");
    client.DefaultRequestHeaders.UserAgent.ParseAdd("MyApp/1.0");
});
options.Services.AddHttpClient("Tavily", client =>
{
    client.Timeout = TimeSpan.FromSeconds(10);
});
// ...
```

**After** — one record per named client, plugin code deleted entirely:

```csharp
[HttpClientOptions]
public sealed record WebFetchHttpClientOptions : IStandardHttpClientOptions
{
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(15);
    public string? UserAgent { get; init; } = "MyApp/1.0";
    public Uri? BaseAddress { get; init; }
    public IReadOnlyDictionary<string, string>? DefaultHeaders { get; init; }
}

[HttpClientOptions(Name = "Brave")]
public sealed record BraveHttpClientOptions : IStandardHttpClientOptions
{
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(15);
    public string? UserAgent { get; init; } = "MyApp/1.0";
    public Uri? BaseAddress { get; init; } = new Uri("https://search.brave.com/");
    public IReadOnlyDictionary<string, string>? DefaultHeaders { get; init; }
}

[HttpClientOptions("Upstream:Tavily")]
public sealed record TavilyHttpClientOptions : INamedHttpClientOptions, IHttpClientTimeout
{
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(10);
}
```

Your `IServiceCollectionPlugin` loses the five `AddHttpClient` blocks entirely. Consumer code — the `IHttpClientFactory.CreateClient("WebFetch")` calls — is unchanged.

## Extensibility (Future Capabilities)

The `[HttpClientOptions]` attribute is intentionally frozen at v1. Future capabilities — resilience, handler chains, handler lifetime, custom `DelegatingHandler`s, HTTP/2 selection, request compression — will ship as **new capability interfaces**, not as new attribute properties. Consumers opt in by implementing the new interface; existing types continue to compile with zero changes.

For example, a hypothetical v2 resilience capability:

```csharp
// v2 adds a new capability interface — ships alongside the existing ones
public interface IHttpClientResilience
{
    HttpResiliencePolicy Resilience { get; }
}

public sealed record HttpResiliencePolicy
{
    public int      MaxRetries     { get; init; } = 3;
    public TimeSpan AttemptTimeout { get; init; } = TimeSpan.FromSeconds(10);
    public TimeSpan TotalTimeout   { get; init; } = TimeSpan.FromSeconds(30);
}

// Existing WebFetch record gains resilience by implementing one more interface
[HttpClientOptions]
public sealed record WebFetchHttpClientOptions
    : IStandardHttpClientOptions, IHttpClientResilience
{
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(15);
    public string? UserAgent { get; init; }
    public Uri? BaseAddress { get; init; }
    public IReadOnlyDictionary<string, string>? DefaultHeaders { get; init; }
    public HttpResiliencePolicy Resilience { get; init; } = new() { MaxRetries = 5 };
}
```

The v2 generator detects the new interface via the same mechanism it uses for Timeout/UserAgent/BaseAddress/Headers today, and emits a single additional wiring block (`builder.AddStandardResilienceHandler(...)`) into the existing `AddHttpClient` callback. Records that don't implement `IHttpClientResilience` are unaffected — no re-compilation, no attribute change, no breaking change.

This trait-composition pattern is the load-bearing design decision. Every future capability follows the same recipe: one new interface, one conditional emission block, zero impact on existing consumers.

## Analyzers

The `HttpClientOptionsAnalyzer` enforces six compile-time contracts. All are **errors** because they would otherwise cause silent runtime wiring bugs.

| ID           | Rule |
|--------------|------|
| NDLRHTTP001  | `[HttpClientOptions]` target must implement `INamedHttpClientOptions` |
| NDLRHTTP002  | Attribute `Name` argument and `ClientName` property resolve to different values |
| NDLRHTTP003  | `ClientName` property body is not a string literal, and no attribute `Name` is supplied |
| NDLRHTTP004  | All three name sources resolve to empty (typically means the type is literally named `HttpClientOptions`) |
| NDLRHTTP005  | Two `[HttpClientOptions]` types in the compilation resolve to the same client name |
| NDLRHTTP006  | `ClientName` property has the wrong shape — must be an instance `string` property with a `get` accessor |

`NDLRHTTP005` is reported at compilation-end so cross-type duplicates are surfaced even when the two types live in different files.

## Attribute Reference

```csharp
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class HttpClientOptionsAttribute : Attribute
{
    public HttpClientOptionsAttribute();
    public HttpClientOptionsAttribute(string sectionName);

    public string? SectionName { get; }       // explicit section, or null to infer
    public string?  Name        { get; set; } // explicit client name override
}
```

| Property    | Type     | Default           | Description |
|-------------|----------|-------------------|-------------|
| `SectionName` | `string?` | null (inferred) | Configuration section path to bind (e.g., `"HttpClients:WebFetch"` or `"Upstream:Tavily"`). If null, the generator infers `"HttpClients:<ResolvedName>"`. |
| `Name`      | `string?` | null (inferred) | Explicit HttpClient name. Highest-precedence name source — overrides a `ClientName` property and type-name inference. |

The attribute does **not** grow in future versions. Additional capabilities ship as new interfaces.
