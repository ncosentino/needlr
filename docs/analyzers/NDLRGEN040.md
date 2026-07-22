# NDLRGEN040: Generated-constructor type shape is unsupported

## Cause

A record type or a nested type carries `[GenerateConstructor]`, or has a field with a positive constructor guard trigger.

## Rule Description

Generated-constructor generation only supports top-level, non-record `partial` classes:

- **Record types** (`record` or `record class`) are unsupported. Records already have their own primary-constructor and positional-parameter conventions that this feature does not attempt to unify with.
- **Nested types** are unsupported. A nested type's enclosing instance (when the outer type is non-static) is not something a generated constructor knows how to obtain.

The diagnostic message reports which of the two unsupported shapes applies: `"a record type"` or `"a nested type"`.

## How to Fix

Move the type to the top level, and use a plain `class` instead of a `record`:

```csharp
using NexusLabs.Needlr.Generators;

// WRONG - NDLRGEN040: record type
[GenerateConstructor]
public partial record UserService
{
    private readonly IRepository _repository;
}

public class Outer
{
    // WRONG - NDLRGEN040: nested type
    [GenerateConstructor]
    public partial class InnerService
    {
        private readonly IRepository _repository;
    }
}

// CORRECT - top-level, non-record partial class
[GenerateConstructor]
public partial class UserService
{
    private readonly IRepository _repository;
}
```

## See Also

- [NDLRGEN039](NDLRGEN039.md) - Generated-constructor type must be partial
- [NDLRGEN042](NDLRGEN042.md) - Generated-constructor base type requires a parameterless constructor
- [Generated Constructors](../generated-constructors.md)
