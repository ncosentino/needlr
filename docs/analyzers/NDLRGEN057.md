# NDLRGEN057: Record constructor-overload type must be partial

## Cause

A property uses `[RecordConstructorOverloadParameter]`, but its positional record is not
declared `partial`.

## Rule Description

Needlr can only add the forwarding constructor through another partial declaration.

## How to Fix

```csharp
// WRONG
public record Request(string Name)
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

## See Also

- [Generated Record Constructor Overloads](../generated-record-constructor-overloads.md)
- [NDLRGEN058](NDLRGEN058.md)
