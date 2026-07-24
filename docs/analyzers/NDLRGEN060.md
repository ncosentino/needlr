# NDLRGEN060: Constructor guard property does not participate in an overload

## Cause

A property has `[ConstructorGuard]` or a constructor-guard alias without
`[RecordConstructorOverloadParameter]`.

## Rule Description

Property guards are emitted only inside generated record constructor overloads. A guard
does not trigger the overload by itself because that would make property participation
implicit.

## How to Fix

```csharp
// WRONG
public partial record Request(string Name)
{
    [ConstructorGuard(ConstructorGuardKind.NotNull)]
    public string? Scope { get; init; }
}
```

```csharp
// CORRECT
public partial record Request(string Name)
{
    [RecordConstructorOverloadParameter]
    [ConstructorGuard(ConstructorGuardKind.NotNull)]
    public string? Scope { get; init; }
}
```

Remove the guard instead when the property should remain outside the generated overload.

## See Also

- [Generated Record Constructor Overloads](../generated-record-constructor-overloads.md)
- [NDLRGEN059](NDLRGEN059.md)
