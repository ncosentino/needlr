# NDLRGEN048: Constructor guard incompatible with field type

## Cause

A built-in `[ConstructorGuard]` kind is applied to a field whose type it cannot validate:

- `NotNullOrEmpty` or `NotNullOrWhiteSpace` applied to a field that is not `string`.
- `NotNull` applied to a field where a runtime `null` value is never possible: a non-nullable value type (other than `Nullable<T>`).

## Rule Description

`NotNullOrEmpty` and `NotNullOrWhiteSpace` call `System.ArgumentException.ThrowIfNullOrEmpty` / `ThrowIfNullOrWhiteSpace`, both of which only accept a `string`. Applying either to a non-`string` field would not compile as generated source, so it is rejected earlier, at the attribute usage site, with a clear message.

`NotNull` is meaningful for any reference type (nullable or not), for `Nullable<T>` value types, and for a type parameter that is not constrained to a value type. It is rejected on a plain non-nullable value type (such as `int` or `Guid`) or a `struct`/`unmanaged`-constrained type parameter because a runtime `null` is never possible for that type -- the guard could never fail, so it only adds dead code.

## How to Fix

Choose a guard kind compatible with the field's type, or change the field's type:

```csharp
using NexusLabs.Needlr.Generators;

[GenerateConstructor]
public partial class UserService
{
    // WRONG - NDLRGEN048: NotNullOrWhiteSpace only applies to string fields
    [ConstructorGuard(ConstructorGuardKind.NotNullOrWhiteSpace)]
    private readonly int _retryCount;

    // WRONG - NDLRGEN048: a non-nullable int can never be null
    [ConstructorGuard(ConstructorGuardKind.NotNull)]
    private readonly int _timeoutSeconds;
}

// CORRECT - NotNullOrWhiteSpace on a string field
[GenerateConstructor]
public partial class UserService
{
    [ConstructorGuard(ConstructorGuardKind.NotNullOrWhiteSpace)]
    private readonly string _tenantName;

    // CORRECT - NotNull on a Nullable<T> value type, where null is possible
    [ConstructorGuard(ConstructorGuardKind.NotNull)]
    private readonly int? _optionalTimeoutSeconds;
}

// CORRECT - an unconstrained type parameter may be a reference type
public partial class ValueHolder<T>
{
    [ConstructorGuard(ConstructorGuardKind.NotNull)]
    private readonly T _value;
}
```

## See Also

- [NDLRGEN047](NDLRGEN047.md) - Invalid constructor guard enum value
- [NDLRGEN051](NDLRGEN051.md) - Custom constructor guard method is invalid
- [Generated Constructors](../generated-constructors.md)
