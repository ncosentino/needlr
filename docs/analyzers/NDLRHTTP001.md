# NDLRHTTP001: [HttpClientOptions] target must implement INamedHttpClientOptions

## Cause

A class decorated with `[HttpClientOptions]` does not implement the `INamedHttpClientOptions` marker interface (directly or transitively via `IStandardHttpClientOptions`).

## Rule Description

`INamedHttpClientOptions` is the contract that opts a type into HttpClient source generation. Needlr's generator discovers `[HttpClientOptions]` types during compilation and emits wiring into the generated `RegisterOptions` method — but only types that implement the marker interface are considered. Without the marker, the attribute is silently useless.

The marker interface is intentionally empty. Actual capabilities (`Timeout`, `UserAgent`, `BaseAddress`, `DefaultHeaders`, and future additions like resilience) are layered via separate capability interfaces the generator detects independently. Every `[HttpClientOptions]` type must implement the marker, but the specific capabilities are opt-in.

## How to Fix

Implement `INamedHttpClientOptions` directly, or implement the convenience aggregate `IStandardHttpClientOptions` which includes the marker plus all v1 capability interfaces:

```csharp
// ❌ Before: generator skips this type
[HttpClientOptions]
public sealed record WebFetchHttpClientOptions
{
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(15);
}

// ✅ After: aggregate interface opts in to all v1 capabilities
[HttpClientOptions]
public sealed record WebFetchHttpClientOptions : IStandardHttpClientOptions
{
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(15);
    public string? UserAgent { get; init; }
    public Uri? BaseAddress { get; init; }
    public IReadOnlyDictionary<string, string>? DefaultHeaders { get; init; }
}

// ✅ Alternative: implement only the capabilities you need
[HttpClientOptions]
public sealed record MinimalHttpClientOptions : INamedHttpClientOptions, IHttpClientTimeout
{
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(15);
}
```

## See Also

- [HttpClient Options](../http-clients.md)
