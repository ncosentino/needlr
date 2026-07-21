# NDLRGEN054: [ConstructorGuardDefinition] guard contract is unresolved

## Cause

The guard type and/or method referenced by `[ConstructorGuardDefinition]` do not resolve to a valid guard method contract -- the same contract required by a direct `[ConstructorGuard(typeof(...))]` usage.

## Rule Description

`[ConstructorGuardDefinition(typeof(GuardType))]` (optionally with an explicit method name) must reference a guard type and method that satisfy the same requirements a direct custom guard usage requires:

- The guard type must resolve and be accessible from the attribute's own declaration.
- An explicit method name, if supplied, must not be empty or white-space-only.
- A compatible accessible static method must exist with the general guard-method shape (`static void Method(T value, string parameterName)`, no `ref`/`out`/`in` parameters) -- checked independently of any specific field type, since the alias can be applied to many different fields later.

Validating the alias once, at its own declaration, means every field that later uses `[CollectionNotEmpty]` (or whatever the alias is named) does not need to re-diagnose the same broken guard reference.

## How to Fix

Fix the referenced guard type or method so it resolves to a valid, accessible, correctly shaped static method:

```csharp
using System;

using NexusLabs.Needlr.Generators;

// WRONG - NDLRGEN054: CollectionNotEmptyGuard has no "Validate" method
public static class CollectionNotEmptyGuard
{
    public static void Check<T>(T value, string parameterName) { }
}

[ConstructorGuardDefinition(typeof(CollectionNotEmptyGuard))]
[AttributeUsage(AttributeTargets.Field)]
public sealed class CollectionNotEmptyAttribute : Attribute
{
}

// CORRECT - either add the conventional "Validate" method...
public static class CollectionNotEmptyGuard
{
    public static void Validate<T>(T value, string parameterName) { }
}

[ConstructorGuardDefinition(typeof(CollectionNotEmptyGuard))]
[AttributeUsage(AttributeTargets.Field)]
public sealed class CollectionNotEmptyAttribute : Attribute
{
}

// CORRECT - ...or reference the actual method name explicitly
[ConstructorGuardDefinition(typeof(CollectionNotEmptyGuard), nameof(CollectionNotEmptyGuard.Check))]
[AttributeUsage(AttributeTargets.Field)]
public sealed class CollectionNotEmptyAttribute : Attribute
{
}
```

## See Also

- [NDLRGEN053](NDLRGEN053.md) - [ConstructorGuardDefinition] target is invalid
- [NDLRGEN051](NDLRGEN051.md) - Custom constructor guard method is invalid
- [Generated Constructors](../generated-constructors.md)
