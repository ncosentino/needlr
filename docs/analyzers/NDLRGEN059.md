# NDLRGEN059: Record constructor-overload property is unsupported

## Cause

A marked property cannot be represented safely by the generated public forwarding
constructor.

## Rule Description

The property must be directly declared, instance, non-indexed, non-positional,
non-required, assignable through `init` or `set`, and typed accessibly enough for the
generated public constructor.

## How to Fix

```csharp
// WRONG
public partial record Request(string Name)
{
    [RecordConstructorOverloadParameter]
    public required int Revision { get; init; }
}
```

```csharp
// CORRECT
public partial record Request(string Name)
{
    [RecordConstructorOverloadParameter]
    public int Revision { get; init; }
}
```

Remove the marker when the property should not participate, or change the property to a
supported shape.

## See Also

- [Generated Record Constructor Overloads](../generated-record-constructor-overloads.md)
- [NDLRGEN058](NDLRGEN058.md)
- [NDLRGEN060](NDLRGEN060.md)
