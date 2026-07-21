# NDLRGEN047: Invalid constructor guard enum value

## Cause

A `[GenerateConstructor]` or `[ConstructorGuard]` argument supplies a `ConstructorNullGuardMode` or `ConstructorGuardKind` value that is not one of the enum's defined members -- for example, a value produced by casting an out-of-range integer.

## Rule Description

`ConstructorGuardKind` and `ConstructorNullGuardMode` arguments must be one of the enum's declared members. Because C# enums are not closed at the type-system level, an expression like `(ConstructorGuardKind)99` compiles but has no defined meaning to the generator. Rather than silently falling back to a default guard behavior, this is rejected at compile time so the generated behavior can never silently diverge from what the source appears to request.

## How to Fix

Use one of the enum's defined members:

```csharp
using NexusLabs.Needlr.Generators;

[GenerateConstructor]
public partial class UserService
{
    // WRONG - NDLRGEN047: 99 is not a defined ConstructorGuardKind value
    [ConstructorGuard((ConstructorGuardKind)99)]
    private readonly string _tenantName;
}

// CORRECT - a defined ConstructorGuardKind member
[GenerateConstructor]
public partial class UserService
{
    [ConstructorGuard(ConstructorGuardKind.NotNullOrWhiteSpace)]
    private readonly string _tenantName;
}
```

## See Also

- [NDLRGEN048](NDLRGEN048.md) - Constructor guard incompatible with field type
- [Generated Constructors](../generated-constructors.md)
