# NDLRGEN050: Custom constructor guard method name is invalid

## Cause

An explicit custom guard method name -- the second argument to `[ConstructorGuard(typeof(...), methodName)]` or `[ConstructorGuardDefinition(typeof(...), methodName)]` -- is empty or consists only of white space.

## Rule Description

When an explicit method name is supplied (instead of relying on the conventional `Validate` method name), it must be a real, non-empty identifier. This is typically supplied through `nameof(...)`, which can never produce an empty string for a real member -- so seeing this diagnostic usually means the method name was supplied as a raw string literal that was left blank or whitespace-only.

## How to Fix

Supply a real method name via `nameof(...)`, or remove the explicit name to fall back to the conventional `Validate` method:

```csharp
using NexusLabs.Needlr.Generators;

public static class NumberGuards
{
    public static void ValidatePositive(int value, string parameterName) { }
}

[GenerateConstructor]
public partial class RetryPolicy
{
    // WRONG - NDLRGEN050: empty explicit method name
    [ConstructorGuard(typeof(NumberGuards), "")]
    private readonly int _retryCount;
}

// CORRECT - nameof() supplies a real method name
[GenerateConstructor]
public partial class RetryPolicy
{
    [ConstructorGuard(typeof(NumberGuards), nameof(NumberGuards.ValidatePositive))]
    private readonly int _retryCount;
}
```

## See Also

- [NDLRGEN049](NDLRGEN049.md) - Custom constructor guard type is invalid
- [NDLRGEN051](NDLRGEN051.md) - Custom constructor guard method is invalid
- [Generated Constructors](../generated-constructors.md)
