# NDLRGEN049: Custom constructor guard type is invalid

## Cause

The `guardType` argument passed to `[ConstructorGuard(typeof(...))]` (or the type referenced by a `[ConstructorGuardDefinition]` alias) cannot be resolved, or is not accessible from the generated constructor.

## Rule Description

A custom constructor guard type must exist and be accessible from the type whose constructor is being generated, because the generated constructor calls it directly -- with no reflection, and therefore no way to work around an inaccessible or missing type at runtime. This diagnostic reports either:

- The type reference could not be resolved (a compile error already exists elsewhere, or the `typeof(...)` expression targets an error type), or
- The type exists, but is not accessible from the generated constructor's containing type -- for example, a `private` guard type nested in an unrelated class.

## How to Fix

Fix the referenced type, or its accessibility, so the guard type is visible to the generated constructor:

```csharp
using NexusLabs.Needlr.Generators;

file static class HiddenGuard // file-scoped: not accessible outside this file
{
    public static void Validate<T>(T value, string parameterName) { }
}

// WRONG - NDLRGEN049: HiddenGuard is not accessible from UserService
[GenerateConstructor]
public partial class UserService
{
    [ConstructorGuard(typeof(HiddenGuard))]
    private readonly string _tenantName;
}

// CORRECT - the guard type is public and accessible
public static class TenantGuard
{
    public static void Validate(string value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
    }
}

[GenerateConstructor]
public partial class UserService
{
    [ConstructorGuard(typeof(TenantGuard))]
    private readonly string _tenantName;
}
```

## See Also

- [NDLRGEN051](NDLRGEN051.md) - Custom constructor guard method is invalid
- [NDLRGEN052](NDLRGEN052.md) - Custom constructor guard method is ambiguous
- [Generated Constructors](../generated-constructors.md)
