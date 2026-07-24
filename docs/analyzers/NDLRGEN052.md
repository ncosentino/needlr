# NDLRGEN052: Custom constructor guard method is ambiguous

## Cause

More than one accessible static method with the guard's method name is compatible with the guarded field or property type on the referenced guard type.

## Rule Description

Overload resolution for a custom guard method is intentionally simple and direct rather than full C# overload resolution: the generator requires exactly one accessible static method compatible with `(value, string parameterName)` for the guarded member's type. When two or more overloads are all compatible -- for example, two overloads that both accept the member's type through different generic constraints, or an exact match plus a compatible base-type overload -- the generator has no principled way to prefer one over the other, so it reports the ambiguity instead of guessing.

## How to Fix

Remove or rename the extra overload(s) so exactly one method matches the guarded member's type, or select the intended overload with an explicit method name:

```csharp
using NexusLabs.Needlr.Generators;

public static class OrderGuards
{
    // WRONG - NDLRGEN052: both overloads are compatible with IReadOnlyCollection<string>
    public static void Validate(IReadOnlyCollection<string> value, string parameterName) { }
    public static void Validate<T>(IReadOnlyCollection<T> value, string parameterName) { }
}

[GenerateConstructor]
public partial class OrderService
{
    [ConstructorGuard(typeof(OrderGuards))]
    private readonly IReadOnlyCollection<string> _orders;
}

// CORRECT - only one overload remains, or select one explicitly by name
public static class OrderGuards
{
    public static void ValidateNotEmpty(IReadOnlyCollection<string> value, string parameterName) { }
}

[GenerateConstructor]
public partial class OrderService
{
    [ConstructorGuard(typeof(OrderGuards), nameof(OrderGuards.ValidateNotEmpty))]
    private readonly IReadOnlyCollection<string> _orders;
}
```

## See Also

- [NDLRGEN051](NDLRGEN051.md) - Custom constructor guard method is invalid
- [NDLRGEN056](NDLRGEN056.md) - Custom constructor guard method is incompatible with forwarded alias arguments
- [Generated Constructors](../generated-constructors.md)
- [Generated Record Constructor Overloads](../generated-record-constructor-overloads.md)
