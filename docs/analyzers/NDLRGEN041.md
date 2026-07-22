# NDLRGEN041: Generated-constructor conflicts with an explicit constructor

## Cause

A class that uses `[GenerateConstructor]`, or has a field with a positive constructor guard trigger, already declares its own explicit instance constructor.

## Rule Description

Generated-constructor generation requires exactly one unambiguous constructor. A type with its own hand-written instance constructor is skipped entirely by the generator -- no source is emitted -- rather than generating a second, conflicting constructor. This diagnostic surfaces that skip as a compile-time error instead of leaving the mismatch silent.

A constructor declared in the generator's own emitted output (a file ending in `.GeneratedConstructor.g.cs`) is never treated as a conflicting hand-written constructor, so this rule only fires for a constructor you actually wrote in source.

## How to Fix

Remove the explicit constructor and let the generator produce it, or remove the generation trigger and keep the hand-written constructor:

```csharp
using NexusLabs.Needlr.Generators;

// WRONG - NDLRGEN041: explicit constructor conflicts with [GenerateConstructor]
[GenerateConstructor]
public partial class UserService
{
    private readonly IRepository _repository;

    public UserService(IRepository repository)
    {
        _repository = repository;
    }
}

// CORRECT - let the generator produce the constructor
[GenerateConstructor]
public partial class UserService
{
    private readonly IRepository _repository;
}

// CORRECT - or keep the hand-written constructor and drop the trigger
public partial class UserService
{
    private readonly IRepository _repository;

    public UserService(IRepository repository)
    {
        _repository = repository;
    }
}
```

## See Also

- [NDLRGEN039](NDLRGEN039.md) - Generated-constructor type must be partial
- [NDLRGEN043](NDLRGEN043.md) - No eligible field for generated-constructor generation
- [Generated Constructors](../generated-constructors.md)
