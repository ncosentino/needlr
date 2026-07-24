# NDLRGEN055: Constructor guard alias usage argument is unsupported

## Cause

A parameterized custom guard alias usage (e.g. `[MinCount(3)]`) supplies a positional constructor argument, or a named argument/property, that this version of Needlr does not forward to the resolved guard method.

## Rule Description

A parameterized custom guard alias automatically forwards every one of its own positional constructor arguments, in declared order, onto the resolved guard method call -- spliced between the guarded value and the trailing `nameof` parameter name. Only the following argument shapes can be forwarded this way: `null`, `bool`, every signed and unsigned integral primitive, `char`, `string`, an `enum` member, and `System.Type` (including an open generic type definition such as `typeof(List<>)`).

Two categories are never forwarded, regardless of where the alias itself is declared:

- **Array/`params` and floating-point (`float`/`double`) positional arguments.** Rendering an array as a single forwarded literal is inherently ambiguous, and floating-point literals can silently lose round-trip precision.
- **Named attribute arguments and properties.** Only positional constructor arguments are ever forwarded.

This is checked at every alias *usage* site (not just the alias's own declaration), because a usage's positional arguments are only known where the alias attribute is actually applied to a participating field or property.

## How to Fix

Change the alias usage to avoid the unsupported argument shape, or forward the value as a supported positional argument instead:

```csharp
using System;

using NexusLabs.Needlr.Generators;

public static class ThresholdGuard
{
    public static void Validate(double value, double threshold, string parameterName) { }
}

[ConstructorGuardDefinition(typeof(ThresholdGuard))]
[AttributeUsage(AttributeTargets.Field)]
public sealed class ThresholdAttribute : Attribute
{
    public ThresholdAttribute(double threshold) { }
}

[GenerateConstructor]
public partial class Container
{
    // WRONG - NDLRGEN055: "threshold" is a double, which is not forwarded in this version
    [Threshold(1.5)]
    private readonly double _value;
}
```

```csharp
using System;

using NexusLabs.Needlr.Generators;

public static class MinCountGuard
{
    public static void Validate<T>(System.Collections.Generic.IReadOnlyCollection<T>? value, int minimum, string parameterName) { }
}

[ConstructorGuardDefinition(typeof(MinCountGuard))]
[AttributeUsage(AttributeTargets.Field)]
public sealed class MinCountAttribute : Attribute
{
    public MinCountAttribute(int minimum) => Minimum = minimum;

    public int Minimum { get; }
}

[GenerateConstructor]
public partial class Container
{
    // CORRECT - "minimum" is an int positional argument, a supported forwarded shape
    [MinCount(3)]
    private readonly System.Collections.Generic.IReadOnlyCollection<string> _value;
}
```

## See Also

- [NDLRGEN056](NDLRGEN056.md) - Custom constructor guard method is incompatible with forwarded alias arguments
- [NDLRGEN051](NDLRGEN051.md) - Custom constructor guard method is invalid
- [Generated Constructors](../generated-constructors.md)
- [Generated Record Constructor Overloads](../generated-record-constructor-overloads.md)
