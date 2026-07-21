# NDLRGEN043: No eligible field for generated-constructor generation

## Cause

A class uses `[GenerateConstructor]`, but declares no field eligible to become a constructor parameter.

## Rule Description

An eligible field is a **private, instance, `readonly`** field **without a field initializer** that is not excluded by `[ConstructorIgnore]`. Generated-constructor generation needs at least one such field to have anything to generate. A class with only static fields, public/internal fields, mutable fields, initialized fields, or properties does not qualify.

## How to Fix

Add at least one eligible field, or remove `[GenerateConstructor]` if the class has nothing for the generator to construct from:

```csharp
using NexusLabs.Needlr.Generators;

// WRONG - NDLRGEN043: no eligible field
[GenerateConstructor]
public partial class UserService
{
    public IRepository Repository { get; set; } // a property, not an eligible field

    private readonly IRepository _initializedRepository = new DefaultRepository(); // has an initializer
}

// CORRECT - an eligible private readonly field without an initializer
[GenerateConstructor]
public partial class UserService
{
    private readonly IRepository _repository;

    public IRepository Repository => _repository;
}
```

## See Also

- [NDLRGEN041](NDLRGEN041.md) - Generated-constructor conflicts with an explicit constructor
- [NDLRGEN046](NDLRGEN046.md) - Constructor guard attribute applied to an ineligible field
- [Generated Constructors](../generated-constructors.md)
