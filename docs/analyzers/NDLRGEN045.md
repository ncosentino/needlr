# NDLRGEN045: Constructor guard attribute has no effect

## Cause

A field carries `[ConstructorIgnore]` or `[ConstructorGuard(ConstructorGuardKind.None)]`, but the containing class has no `[GenerateConstructor]` and no other field has a positive constructor guard trigger -- so no constructor is generated at all, and the exclusion-only attribute has nothing to exclude the field from.

## Rule Description

`[ConstructorIgnore]` and a bare `[ConstructorGuard(ConstructorGuardKind.None)]` are **exclusion-only modifiers**. They only make sense when some other trigger already causes constructor generation for the class:

- `[GenerateConstructor]` on the class, or
- A positive constructor guard trigger (a built-in guard kind other than `None`, a custom guard type, or an alias attribute) on some other field of the same class.

Applying either exclusion-only attribute without one of those triggers present produces no generated constructor, so the attribute is a no-op. This is a warning rather than an error because it does not prevent compilation -- it just means the attribute is dead code.

## How to Fix

Either remove the attribute, or add a generation trigger:

```csharp
using NexusLabs.Needlr.Generators;

// WARNING: WRONG (warning) - NDLRGEN045: no [GenerateConstructor] and no other positive guard trigger
public partial class CacheEntry
{
    private readonly IRepository _repository;

    [ConstructorIgnore]
    private readonly string? _serializedPayload;
}

// CORRECT - add [GenerateConstructor] so the exclusion has an effect
[GenerateConstructor]
public partial class CacheEntry
{
    private readonly IRepository _repository;

    [ConstructorIgnore]
    private readonly string? _serializedPayload;
}

// CORRECT - or a positive guard on another field already triggers generation
public partial class CacheEntry
{
    [ConstructorGuard(ConstructorGuardKind.NotNull)]
    private readonly IRepository _repository;

    [ConstructorIgnore]
    private readonly string? _serializedPayload;
}
```

## See Also

- [NDLRGEN046](NDLRGEN046.md) - Constructor guard attribute applied to an ineligible field
- [Generated Constructors](../generated-constructors.md)
