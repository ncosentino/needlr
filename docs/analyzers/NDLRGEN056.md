# NDLRGEN056: Custom constructor guard method is incompatible with forwarded alias arguments

## Cause

The guard method resolved for a parameterized custom guard alias usage does not have exactly one middle parameter per forwarded argument, or one of its middle parameters is not compatible with the type of the argument forwarded to that position.

## Rule Description

A parameterized custom guard alias usage (e.g. `[MinCount(3)]`) forwards its own positional constructor arguments between the guarded value and the trailing `string` parameter name. The resolved guard method must be shaped as `static void Method(T value, ...one parameter per forwarded argument, string parameterName)`:

- **Effective arity must match exactly.** The number of parameters between the value and the trailing parameter name must equal the number of arguments the alias usage forwards -- neither more nor fewer.
- **Each middle parameter's type must be compatible with its corresponding forwarded argument's type**, either directly or, for a generic guard method, via type-parameter unification with the field's type (the same unification `MinCountGuard.Validate<T>` uses to bind `T` from the field's collection element type).

This diagnostic is reported specifically when the incompatibility is attributable to a forwarded argument. A value-parameter or general-shape incompatibility unrelated to any forwarded argument is still reported by [NDLRGEN051](NDLRGEN051.md), and an ambiguous match across multiple compatible overloads is still reported by [NDLRGEN052](NDLRGEN052.md).

## How to Fix

Change the number or types of the alias usage's forwarded arguments, or the guard method's middle parameters, so they match exactly:

```csharp
using System.Collections.Generic;

using NexusLabs.Needlr.Generators;

// WRONG - NDLRGEN056: MinCountGuard.Validate has one middle parameter (minimum),
// but [WithinRange] forwards two arguments (min, max)
public static class MinCountGuard
{
    public static void Validate<T>(IReadOnlyCollection<T>? value, int minimum, string parameterName) { }
}

[ConstructorGuardDefinition(typeof(MinCountGuard))]
[AttributeUsage(AttributeTargets.Field)]
public sealed class WithinRangeAttribute : Attribute
{
    public WithinRangeAttribute(int min, int max) { }
}

[GenerateConstructor]
public partial class Container
{
    [WithinRange(3, 10)]
    private readonly IReadOnlyCollection<string> _value;
}
```

```csharp
using System.Collections.Generic;

using NexusLabs.Needlr.Generators;

// CORRECT - the guard method declares exactly one middle parameter per forwarded argument,
// each compatible with its corresponding argument's type
public static class RangeGuard
{
    public static void Validate<T>(IReadOnlyCollection<T>? value, int min, int max, string parameterName) { }
}

[ConstructorGuardDefinition(typeof(RangeGuard))]
[AttributeUsage(AttributeTargets.Field)]
public sealed class WithinRangeAttribute : Attribute
{
    public WithinRangeAttribute(int min, int max) { }
}

[GenerateConstructor]
public partial class Container
{
    [WithinRange(3, 10)]
    private readonly IReadOnlyCollection<string> _value;
}
```

## See Also

- [NDLRGEN055](NDLRGEN055.md) - Constructor guard alias usage argument is unsupported
- [NDLRGEN051](NDLRGEN051.md) - Custom constructor guard method is invalid
- [NDLRGEN052](NDLRGEN052.md) - Custom constructor guard method is ambiguous
- [Generated Constructors](../generated-constructors.md)
