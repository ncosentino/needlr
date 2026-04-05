# NDLRHTTP003: ClientName property body is not a literal expression

## Cause

A `[HttpClientOptions]` type has a `ClientName` property whose body cannot be statically evaluated to a string literal at compile time, and the attribute does not supply a `Name` argument as a fallback.

## Rule Description

HttpClient names are resolved at compile time because the generator emits `services.AddHttpClient("name", ...)` into source — well before the DI container exists at runtime. The generator reads the syntax tree of the `ClientName` property and extracts the literal value from either an expression body (`=> "foo"`), a single-return getter body, or an auto-property initializer.

Anything more complex — a method call, a field reference, a string interpolation, a conditional expression — cannot be resolved by the generator. Rather than silently fall through to type-name inference and produce a mystery client, the analyzer fails loudly.

## How to Fix

Use either a literal expression body on the property, or set the attribute `Name` argument explicitly:

```csharp
// ❌ Before: computed at runtime, generator can't see the value
[HttpClientOptions]
public sealed record WebFetchHttpClientOptions : IStandardHttpClientOptions
{
    public string ClientName => ComputeName();
    private static string ComputeName() => "WebFetch";
    // ...
}

// ✅ After (option A): literal expression body
[HttpClientOptions]
public sealed record WebFetchHttpClientOptions : IStandardHttpClientOptions
{
    public string ClientName => "WebFetch";
    // ...
}

// ✅ After (option B): drop the property, use attribute argument
[HttpClientOptions(Name = "WebFetch")]
public sealed record WebFetchHttpClientOptions : IStandardHttpClientOptions
{
    // ...
}
```

## See Also

- [HttpClient Options](../http-clients.md)
- [NDLRHTTP002 - HttpClient name sources conflict](NDLRHTTP002.md)
