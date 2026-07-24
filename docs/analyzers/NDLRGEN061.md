# NDLRGEN061: Record constructor overload conflicts with field-based generation

## Cause

The same type combines `[RecordConstructorOverloadParameter]` with
`[GenerateConstructor]` or a positive field-level generated-constructor trigger.

## Rule Description

The two features have different ownership:

- field-based generation owns an ordinary class constructor and Needlr DI metadata;
- record-overload generation adds one forwarding constructor to a positional record and
  remains outside DI metadata.

Needlr does not compose those constructor models implicitly.

## How to Fix

```csharp
// WRONG
[GenerateConstructor]
public partial record Request(string Name)
{
    [RecordConstructorOverloadParameter]
    public int Revision { get; init; }
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

For an ordinary class, remove the record marker and use eligible private `readonly` fields
with `[GenerateConstructor]`.

## See Also

- [Generated Record Constructor Overloads](../generated-record-constructor-overloads.md)
- [Generated Constructors](../generated-constructors.md)
- [NDLRGEN058](NDLRGEN058.md)
