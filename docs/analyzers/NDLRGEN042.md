# NDLRGEN042: Generated-constructor base type requires a parameterless constructor

## Cause

A class using generated-constructor generation derives from a base type that has no accessible parameterless constructor.

## Rule Description

The generated constructor relies on the implicit `: base()` call -- a source generator cannot supply base-constructor arguments on your behalf. A base type is supported when it either:

- Declares no explicit constructors at all (the implicit parameterless constructor applies), or
- Declares an explicit parameterless constructor that is `public`, `protected`, or `protected internal`.

This supports common framework base types such as `BackgroundService`, which has no explicit constructors. A base type that *only* declares constructors requiring arguments is unsupported.

## How to Fix

Add an accessible parameterless constructor to the base type, derive from a base type that already has one (or from `object` directly), or remove the generation trigger and write the constructor by hand:

```csharp
using NexusLabs.Needlr.Generators;

public abstract class ServiceBase
{
    protected ServiceBase(string name) { /* ... */ }
}

// WRONG - NDLRGEN042: ServiceBase has no accessible parameterless constructor
[GenerateConstructor]
public partial class UserService : ServiceBase
{
    private readonly IRepository _repository;
}

// CORRECT - add an accessible parameterless constructor to the base type
public abstract class ServiceBase
{
    protected ServiceBase() { }
    protected ServiceBase(string name) { /* ... */ }
}

[GenerateConstructor]
public partial class UserService : ServiceBase
{
    private readonly IRepository _repository;
}

// CORRECT - or derive from a base type with an implicit parameterless constructor
[GenerateConstructor]
public partial class MyWorker : BackgroundService
{
    private readonly IRepository _repository;

    protected override Task ExecuteAsync(CancellationToken stoppingToken) => Task.CompletedTask;
}
```

## See Also

- [NDLRGEN040](NDLRGEN040.md) - Generated-constructor type shape is unsupported
- [NDLRGEN041](NDLRGEN041.md) - Generated-constructor conflicts with an explicit constructor
- [Generated Constructors](../generated-constructors.md)
