# NDLRHTTP002: HttpClient name sources conflict

## Cause

A `[HttpClientOptions]` type specifies a client name in both the attribute's `Name` argument and a `ClientName` property on the type, and the two values disagree.

## Rule Description

The HttpClient name can come from three sources, resolved in strict precedence order: (1) the attribute's `Name` argument, (2) a literal `ClientName` property on the type, (3) inferred from the type name via suffix stripping. When both an attribute argument and a literal `ClientName` property are present, they must resolve to the same value — otherwise the generator cannot pick silently, and one author's intent would be ignored.

This diagnostic fires before generation so the conflict is surfaced immediately instead of producing a client registered under an unexpected name.

## How to Fix

Pick one source of truth and remove the other:

```csharp
// ❌ Before: attribute says "tavily-v2" but property says "tavily-v1"
[HttpClientOptions(Name = "tavily-v2")]
public sealed record TavilyHttpClientOptions : IStandardHttpClientOptions
{
    public string ClientName => "tavily-v1";
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(10);
    // ...
}

// ✅ After (option A): keep the attribute, delete the property
[HttpClientOptions(Name = "tavily-v2")]
public sealed record TavilyHttpClientOptions : IStandardHttpClientOptions
{
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(10);
    // ...
}

// ✅ After (option B): keep the property, remove Name from the attribute
[HttpClientOptions]
public sealed record TavilyHttpClientOptions : IStandardHttpClientOptions
{
    public string ClientName => "tavily-v1";
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(10);
    // ...
}
```

## See Also

- [HttpClient Options](../http-clients.md)
- [NDLRHTTP003 - ClientName property body is not a literal](NDLRHTTP003.md)
