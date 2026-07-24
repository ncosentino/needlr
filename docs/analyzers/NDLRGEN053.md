# NDLRGEN053: [ConstructorGuardDefinition] target is invalid

## Cause

`[ConstructorGuardDefinition]` decorates a type that is not a usable constructor-guard
alias: it is not derived from `System.Attribute`, or its own `[AttributeUsage]` allows
neither `AttributeTargets.Field` nor `AttributeTargets.Property`.

## Rule Description

`[ConstructorGuardDefinition]` turns an application-defined attribute type into an alias for a constructor guard, so it can be applied to a generated-constructor field or a participating record-overload property. That only makes sense when the decorated type:

- **Is itself an `Attribute`-derived type** (so it can legally be applied as an attribute at all), and
- **Allows `AttributeTargets.Field` or `AttributeTargets.Property`** in its own `[AttributeUsage]`.

The diagnostic message reports which requirement failed.

## How to Fix

Derive from `Attribute` and allow at least one supported member target:

```csharp
using System;

using NexusLabs.Needlr.Generators;

// WRONG - NDLRGEN053: not derived from System.Attribute
[ConstructorGuardDefinition(typeof(CollectionNotEmptyGuard))]
public sealed class CollectionNotEmptyAttribute
{
}

// WRONG - NDLRGEN053: neither supported member target is allowed
[ConstructorGuardDefinition(typeof(CollectionNotEmptyGuard))]
[AttributeUsage(AttributeTargets.Method)]
public sealed class CollectionNotEmptyAttribute : Attribute
{
}

// CORRECT - derived from Attribute, usable on fields and participating properties
[ConstructorGuardDefinition(typeof(CollectionNotEmptyGuard))]
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
public sealed class CollectionNotEmptyAttribute : Attribute
{
}
```

## See Also

- [NDLRGEN054](NDLRGEN054.md) - [ConstructorGuardDefinition] guard contract is unresolved
- [NDLRGEN055](NDLRGEN055.md) - Constructor guard alias usage argument is unsupported
- [Generated Constructors](../generated-constructors.md)
- [Generated Record Constructor Overloads](../generated-record-constructor-overloads.md)
