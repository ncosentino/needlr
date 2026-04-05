# NDLRHTTP004: Resolved HttpClient name is empty

## Cause

A `[HttpClientOptions]` type has no attribute `Name` argument, no literal `ClientName` property, and the type name produces an empty string when the generator's suffix-stripping inference runs.

## Rule Description

The generator resolves HttpClient names from three sources in precedence order. If the attribute and property are both absent, the fallback strips the suffixes `HttpClientOptions`, `HttpClientSettings`, or `HttpClient` from the type name. When the type is literally named `HttpClientOptions` (or one of the other stripped suffixes) the fallback yields an empty string, and the generator has nothing to register the client under.

This rarely happens in practice — most types carry a meaningful prefix — but the analyzer catches it at compile time so you never ship an `AddHttpClient("", ...)` registration.

## How to Fix

Rename the type to carry a meaningful prefix, or supply a `Name` explicitly in the attribute:

```csharp
// ❌ Before: suffix stripping produces ""
[HttpClientOptions]
public sealed record HttpClientOptions : IStandardHttpClientOptions
{
    // ...
}

// ✅ After (option A): rename the type
[HttpClientOptions]
public sealed record WebFetchHttpClientOptions : IStandardHttpClientOptions
{
    // ...
}

// ✅ After (option B): specify the name in the attribute
[HttpClientOptions(Name = "default")]
public sealed record HttpClientDefaults : IStandardHttpClientOptions
{
    // ...
}
```

## See Also

- [HttpClient Options](../http-clients.md)
