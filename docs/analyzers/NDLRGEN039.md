# NDLRGEN039: Generated-constructor type must be partial

## Cause

A class uses `[GenerateConstructor]`, or has a field with a positive constructor guard trigger, but the class itself is not declared `partial`.

## Rule Description

Roslyn source generators can only contribute new members -- such as a generated constructor -- to a type declared `partial`. Needlr's generated-constructor feature therefore requires the containing class to be `partial` so `GeneratedConstructorGenerator` can add the constructor as a separate compilation unit.

This diagnostic fires whenever a type is otherwise eligible for generated-constructor generation (it has `[GenerateConstructor]`, or a field with a positive `[ConstructorGuard]`/alias trigger) but is missing the `partial` modifier.

## How to Fix

Add the `partial` modifier to the class declaration:

```csharp
using NexusLabs.Needlr.Generators;

// WRONG - NDLRGEN039: class is not partial
[GenerateConstructor]
public class UserService
{
    private readonly IRepository _repository;
}

// CORRECT - class is declared partial
[GenerateConstructor]
public partial class UserService
{
    private readonly IRepository _repository;
}
```

## See Also

- [NDLRGEN040](NDLRGEN040.md) - Generated-constructor type shape is unsupported (record or nested type)
- [NDLRGEN041](NDLRGEN041.md) - Generated-constructor conflicts with an explicit constructor
- [Generated Constructors](../generated-constructors.md)
