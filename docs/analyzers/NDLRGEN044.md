# NDLRGEN044: Generated-constructor parameter names collide

## Cause

Two or more eligible fields on the same class normalize to the same generated constructor parameter name.

## Rule Description

Field names are normalized to constructor parameter names by removing a leading underscore and lower-casing the first letter (for example, `_repository` becomes `repository`). Two fields that normalize to the same name -- for example `_repository` and `Repository` (a non-conventional but still-eligible private readonly field) -- make the generated constructor's parameter list ambiguous, so generation is skipped entirely.

## How to Fix

Rename one of the conflicting fields so their normalized parameter names are distinct:

```csharp
using NexusLabs.Needlr.Generators;

// WRONG - NDLRGEN044: both fields normalize to the parameter name "repository"
[GenerateConstructor]
public partial class UserService
{
    private readonly IRepository _repository;
    private readonly IRepository Repository;
}

// CORRECT - distinct normalized parameter names
[GenerateConstructor]
public partial class UserService
{
    private readonly IRepository _repository;
    private readonly IRepository _fallbackRepository;
}
```

## See Also

- [NDLRGEN043](NDLRGEN043.md) - No eligible field for generated-constructor generation
- [Generated Constructors](../generated-constructors.md)
