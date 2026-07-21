# NDLRGEN053: [ConstructorGuardDefinition] target is invalid

## Cause

`[ConstructorGuardDefinition]` decorates a type that is not a usable field-attribute alias: it is not derived from `System.Attribute`, or its own `[AttributeUsage]` does not allow `AttributeTargets.Field`.

## Rule Description

`[ConstructorGuardDefinition]` turns an application-defined attribute type into an alias for a constructor guard, so it can be applied to a field the same way `[ConstructorGuard]` is. That only makes sense when the decorated type:

- **Is itself an `Attribute`-derived type** (so it can legally be applied as an attribute at all), and
- **Allows `AttributeTargets.Field`** in its own `[AttributeUsage]` (so it can legally be applied to the fields this feature targets).

The diagnostic message reports which requirement failed: `"not derived from System.Attribute"` or `"not usable on fields ([AttributeUsage] does not include AttributeTargets.Field)"`.

## How to Fix

Derive from `Attribute` and allow `AttributeTargets.Field`:

```csharp
using System;

using NexusLabs.Needlr.Generators;

// WRONG - NDLRGEN053: not derived from System.Attribute
[ConstructorGuardDefinition(typeof(CollectionNotEmptyGuard))]
public sealed class CollectionNotEmptyAttribute
{
}

// WRONG - NDLRGEN053: [AttributeUsage] does not include AttributeTargets.Field
[ConstructorGuardDefinition(typeof(CollectionNotEmptyGuard))]
[AttributeUsage(AttributeTargets.Property)]
public sealed class CollectionNotEmptyAttribute : Attribute
{
}

// CORRECT - derived from Attribute, usable on fields
[ConstructorGuardDefinition(typeof(CollectionNotEmptyGuard))]
[AttributeUsage(AttributeTargets.Field)]
public sealed class CollectionNotEmptyAttribute : Attribute
{
}
```

## See Also

- [NDLRGEN054](NDLRGEN054.md) - [ConstructorGuardDefinition] guard contract is unresolved
- [Generated Constructors](../generated-constructors.md)
