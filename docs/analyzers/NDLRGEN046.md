# NDLRGEN046: Constructor guard attribute applied to an ineligible field

## Cause

A `[ConstructorGuard]`, `[ConstructorIgnore]`, or alias guard attribute is applied to a field that cannot participate in generated-constructor generation at all.

## Rule Description

Only a field that is **private**, **instance**, **`readonly`**, and declared **without a field initializer** can become a generated-constructor parameter. A guard attribute on any other kind of field has no effect on generation and is reported as an error, since the field will never appear in the generated constructor no matter what the attribute requests.

The diagnostic message reports the specific reason the field is ineligible, one of:

| Reason | Field shape |
|--------|-------------|
| `compiler-generated` | An implicitly declared field the compiler generated (e.g. behind an auto-property) |
| `static` | A `static` field |
| `not private` | `public`, `internal`, or `protected` accessibility |
| `not readonly` | A mutable field |
| `initialized with a field initializer` | Declared with `= ...` |

## How to Fix

Change the field's declaration so it is eligible, or remove the guard attribute:

```csharp
using NexusLabs.Needlr.Generators;

[GenerateConstructor]
public partial class UserService
{
    // WRONG - NDLRGEN046: "not private"
    [ConstructorGuard(ConstructorGuardKind.NotNull)]
    public readonly IRepository Repository;

    // WRONG - NDLRGEN046: "not readonly"
    [ConstructorGuard(ConstructorGuardKind.NotNull)]
    private IRepository _mutableRepository;

    // WRONG - NDLRGEN046: "initialized with a field initializer"
    [ConstructorGuard(ConstructorGuardKind.NotNull)]
    private readonly IRepository _defaultedRepository = new DefaultRepository();
}

// CORRECT - private, instance, readonly, no initializer
[GenerateConstructor]
public partial class UserService
{
    [ConstructorGuard(ConstructorGuardKind.NotNull)]
    private readonly IRepository _repository;
}
```

## See Also

- [NDLRGEN043](NDLRGEN043.md) - No eligible field for generated-constructor generation
- [NDLRGEN045](NDLRGEN045.md) - Constructor guard attribute has no effect
- [Generated Constructors](../generated-constructors.md)
