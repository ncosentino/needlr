# NDLRGEN058: Record constructor-overload type shape is unsupported

## Cause

`[RecordConstructorOverloadParameter]` is used outside a supported top-level partial
positional record class.

## Rule Description

Ordinary classes, record structs, body-only records, file-local records, nested records,
and inherited records cannot use this feature.

## How to Fix

```csharp
// WRONG
public partial class Request
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

Use [`[GenerateConstructor]`](../generated-constructors.md) for field-derived constructors
on ordinary partial classes.

## See Also

- [Generated Record Constructor Overloads](../generated-record-constructor-overloads.md)
- [NDLRGEN057](NDLRGEN057.md)
- [NDLRGEN061](NDLRGEN061.md)
