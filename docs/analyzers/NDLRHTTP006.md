# NDLRHTTP006: ClientName property has wrong shape

## Cause

A `[HttpClientOptions]` type has a `ClientName` property that is not an instance property of type `string` with a readable `get` accessor.

## Rule Description

The generator recognizes exactly one shape for the `ClientName` name-source property: an instance (non-static), readable, `string`-typed property. Static members, non-string types, and write-only properties are rejected so they cannot silently fall through to type-name inference and produce a surprising client name.

This is a shape check only — it fires regardless of whether the property body is a literal. The NDLRHTTP003 diagnostic handles the case where the shape is valid but the body isn't statically resolvable.

## How to Fix

Make `ClientName` a readable instance property of type `string`, either with an expression body or as an auto-property with an initializer:

```csharp
// ❌ Before: static — not an instance member
[HttpClientOptions]
public sealed record WebFetchHttpClientOptions : IStandardHttpClientOptions
{
    public static string ClientName => "WebFetch";
    // ...
}

// ❌ Before: wrong type
[HttpClientOptions]
public sealed record WebFetchHttpClientOptions : IStandardHttpClientOptions
{
    public int ClientName => 42;
    // ...
}

// ✅ After: instance, readable, string
[HttpClientOptions]
public sealed record WebFetchHttpClientOptions : IStandardHttpClientOptions
{
    public string ClientName => "WebFetch";
    // ...
}
```

If you do not need a `ClientName` property at all (because suffix-stripping inference or the attribute `Name` argument suffices), simply remove it.

## See Also

- [HttpClient Options](../http-clients.md)
- [NDLRHTTP003 - ClientName property body is not a literal](NDLRHTTP003.md)
