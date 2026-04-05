# NDLRHTTP005: Duplicate HttpClient name

## Cause

Two or more `[HttpClientOptions]` types in the same compilation resolve to the same HttpClient name.

## Rule Description

`services.AddHttpClient("name", ...)` registrations are keyed by name. If two options types resolve to the same name, the second registration silently overwrites the first, and at runtime `IHttpClientFactory.CreateClient("name")` returns whichever configuration won the race. The resulting behavior depends on emission order and is non-deterministic from the consumer's perspective.

The analyzer detects this via a compilation-end action that aggregates every resolved name across the whole compilation. Duplicates are surfaced on every participant after the first, pointing at the prior participant, so both ends of the clash show as IDE squiggles.

This also catches cross-file duplicates where each file in isolation looks correct.

## How to Fix

Give each client a unique name. You can use the attribute `Name` argument to disambiguate types that would otherwise infer the same name:

```csharp
// ❌ Before: both infer to "WebFetch"
namespace SearchFeatures;

[HttpClientOptions]
public sealed record WebFetchHttpClientOptions : IStandardHttpClientOptions { /* ... */ }

namespace CacheFeatures;

[HttpClientOptions]
public sealed record WebFetchHttpClientOptions : IStandardHttpClientOptions { /* ... */ }

// ✅ After: disambiguate via attribute Name
namespace SearchFeatures;

[HttpClientOptions(Name = "search-webfetch")]
public sealed record WebFetchHttpClientOptions : IStandardHttpClientOptions { /* ... */ }

namespace CacheFeatures;

[HttpClientOptions(Name = "cache-webfetch")]
public sealed record WebFetchHttpClientOptions : IStandardHttpClientOptions { /* ... */ }
```

## See Also

- [HttpClient Options](../http-clients.md)
