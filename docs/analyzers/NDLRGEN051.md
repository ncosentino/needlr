# NDLRGEN051: Custom constructor guard method is invalid

## Cause

No accessible static method matching the required guard-method shape was found on the referenced guard type for the field's type.

## Rule Description

A custom guard method must be an **accessible**, **static**, **`void`**-returning method with **exactly two parameters**: a value parameter compatible with the field's type (directly, or via a generic method's type-parameter inference), and a `string` parameter name as the last parameter. Neither parameter may be passed by `ref`, `out`, or `in` -- the generator emits a direct call and cannot support by-reference parameters.

The generator calls this method directly, so its shape must be fully resolvable at compile time; there is no fallback to reflection for a method that almost matches. The diagnostic message reports the specific reason the best candidate method failed to qualify, for example:

- No method with that name exists on the guard type.
- The method is not accessible from the generated constructor.
- The method is not `static`.
- The method does not return `void`.
- The method does not have exactly two parameters.
- The method's second parameter is not a `string` parameter name.
- A parameter is passed by `ref`/`out`/`in`.
- The method's value parameter type is not compatible with the field's type.

## How to Fix

Adjust the guard method's signature to match the required shape:

```csharp
using NexusLabs.Needlr.Generators;

public static class CollectionNotEmptyGuard
{
    // WRONG - NDLRGEN051: not static
    public void Validate(IReadOnlyCollection<string> value, string parameterName) { }
}

[GenerateConstructor]
public partial class OrderService
{
    [ConstructorGuard(typeof(CollectionNotEmptyGuard))]
    private readonly IReadOnlyCollection<string> _orders;
}
```

```csharp
using NexusLabs.Needlr.Generators;

// CORRECT - accessible, static, void, (value, string parameterName)
public static class CollectionNotEmptyGuard
{
    public static void Validate<T>(IReadOnlyCollection<T>? value, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(value, parameterName);

        if (value.Count == 0)
        {
            throw new ArgumentException("Collection must not be empty.", parameterName);
        }
    }
}

[GenerateConstructor]
public partial class OrderService
{
    [ConstructorGuard(typeof(CollectionNotEmptyGuard))]
    private readonly IReadOnlyCollection<string> _orders;
}
```

## See Also

- [NDLRGEN049](NDLRGEN049.md) - Custom constructor guard type is invalid
- [NDLRGEN052](NDLRGEN052.md) - Custom constructor guard method is ambiguous
- [Generated Constructors](../generated-constructors.md)
