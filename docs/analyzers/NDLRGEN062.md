# NDLRGEN062: Record constructor-overload signature collides

## Cause

The proposed forwarding constructor has the same C# signature as an existing explicit or
synthesized constructor.

## Rule Description

Constructor signature identity ignores parameter names, nullable reference annotations,
`params`, optional values, and `dynamic` versus `object`. Needlr emits no overload when
those normalized parameter types and passing modes collide.

## How to Fix

```csharp
// WRONG
public partial record Request(string Name)
{
    [RecordConstructorOverloadParameter]
    public string? Scope { get; init; }

    public Request(string Name, string Scope) : this(Name)
    {
        this.Scope = Scope;
    }
}
```

```csharp
// CORRECT - keep either the generated overload or the explicit constructor, not both
public partial record Request(string Name)
{
    [RecordConstructorOverloadParameter]
    public string? Scope { get; init; }
}
```

Changing parameter names or nullable annotations does not resolve a collision; the
runtime parameter types must differ.

## See Also

- [Generated Record Constructor Overloads](../generated-record-constructor-overloads.md)
- [NDLRGEN059](NDLRGEN059.md)
