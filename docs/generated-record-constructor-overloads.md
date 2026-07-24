---
description: Generate one guarded forwarding constructor overload for a partial positional record from explicitly marked properties.
---

# Generated Record Constructor Overloads

`[RecordConstructorOverloadParameter]` removes the repeated parameter list, `this(...)`
forwarding, guard clauses, assignments, and XML documentation required when a positional
record needs an additional construction-time property.

The property marker is the complete opt-in. There is no class-level generation attribute.

## Quick Start

```csharp
using NexusLabs.Needlr.Generators;

public sealed partial record PreparedRequest(
    string Query,
    string Tenant)
{
    /// <summary>Gets the prepared access scope.</summary>
    [RecordConstructorOverloadParameter]
    [ConstructorGuard(ConstructorGuardKind.NotNull)]
    public PreparedScope? PreparedScope { get; init; }
}
```

Needlr preserves the positional primary constructor and adds:

```csharp
partial record class PreparedRequest
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PreparedRequest"/> record by
    /// forwarding its positional parameters and assigning its marked properties.
    /// </summary>
    /// <param name="Query">The value forwarded to the positional primary constructor.</param>
    /// <param name="Tenant">The value forwarded to the positional primary constructor.</param>
    /// <param name="PreparedScope">Gets the prepared access scope.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="PreparedScope"/> is null.
    /// </exception>
    public PreparedRequest(
        string Query,
        string Tenant,
        PreparedScope PreparedScope)
        : this(Query, Tenant)
    {
        global::System.ArgumentNullException.ThrowIfNull(PreparedScope);

        this.PreparedScope = PreparedScope;
    }
}
```

The nullable property remains nullable because the original primary-constructor path may
leave it unset. The generated overload parameter is nonnullable because its effective
`NotNull` guard rejects null.

## Why a Separate Record Feature?

[`[GenerateConstructor]`](generated-constructors.md) generates the complete constructor
for an ordinary partial class from private `readonly` fields. It also shares its constructor
model with Needlr's DI and NativeAOT activation metadata.

A positional record already owns a primary constructor. Needlr cannot rewrite that
constructor, and adding a forwarding overload must not change DI constructor selection.
The record-only marker therefore has a separate contract:

- it adds one overload instead of replacing a construction path;
- it operates on explicitly marked properties instead of fields;
- it never participates in Needlr DI, factory, or service-catalog metadata;
- using it together with `[GenerateConstructor]` is an error.

See [ADR-0006](adr/adr-0006-generate-record-constructor-overloads.md) for the decision.

## Parameter Ordering

The generated overload contains:

1. every positional primary-constructor parameter in its declared order; then
2. every marked property ordered by source file path and source position.

All marked properties participate in one overload. Needlr does not generate combinations
of optional overloads.

```csharp
public partial record Request(string Name)
{
    [RecordConstructorOverloadParameter]
    public int Revision { get; init; }

    [RecordConstructorOverloadParameter]
    public string? Scope { get; init; }
}
```

Generated signature:

```csharp
public Request(string Name, int Revision, string? Scope)
    : this(Name)
```

Primary `params` parameters are emitted as their array type because marked property
parameters follow them. Primary optional/default values are not copied to the overload.
Generic types, tuples, nullability, and escaped identifiers are preserved.

## Guards

Marked properties support the same guards as generated class constructors:

- `NotNull`
- `NotNullOrEmpty`
- `NotNullOrWhiteSpace`
- direct custom guard types
- explicit custom guard methods
- application-defined aliases
- parameterized aliases such as `[MinLength(3)]`

All guard calls are resolved at compile time and emitted as direct, fully qualified C#.
No reflection is used.

```csharp
public partial record Request(string Name)
{
    [RecordConstructorOverloadParameter]
    [ConstructorGuard(ConstructorGuardKind.NotNullOrWhiteSpace)]
    public string? Tenant { get; init; }
}
```

A constructor guard on a property does not trigger generation by itself. The property must
also carry `[RecordConstructorOverloadParameter]`; otherwise
[NDLRGEN060](analyzers/NDLRGEN060.md) is reported.

## Nullable Storage and Nonnullable Parameters

When a marked nullable reference property has an effective built-in null-rejecting guard,
Needlr removes only the parameter's top-level nullable annotation:

```csharp
[RecordConstructorOverloadParameter]
[ConstructorGuard(ConstructorGuardKind.NotNull)]
public IReadOnlyList<string?>? Values { get; init; }
```

Generated parameter:

```csharp
IReadOnlyList<string?> Values
```

Nested nullability remains unchanged. Custom guards do not imply a nonnullable parameter
because Needlr cannot infer arbitrary custom guard semantics.

## Record Semantics Are Unchanged

The guard belongs only to the generated overload:

```csharp
var guarded = new PreparedRequest("query", "tenant", preparedScope);

var cleared = guarded with { PreparedScope = null }; // Legal

var initialized = new PreparedRequest("query", "tenant")
{
    PreparedScope = null, // Legal
};
```

The generated overload does not intercept:

- the positional primary constructor;
- copy construction;
- object initializers;
- `with` expressions;
- serializers or materializers using another path.

Use a different domain model or validation boundary when an invariant must hold across
every mutation and deserialization path.

## Supported Types and Properties

The containing type must be a top-level, non-file-local, partial positional `record class`.
Sealed, unsealed, and generic records are supported.

A marked property must:

- be declared directly by the record;
- be an instance, non-indexer property;
- have an assignable `init` or `set` accessor;
- not be synthesized from a positional parameter;
- not be `required`;
- use a type accessible from the generated public constructor.

Record structs, body-only records, nested records, inherited records, ordinary classes,
and invalid properties are diagnosed and produce no overload.

## Constructor Collisions

Needlr compares the complete proposed parameter sequence with every existing constructor.
Parameter names, nullable reference annotations, `params`, `dynamic` versus `object`, and
optional values do not create distinct C# signatures.

If the overload would collide with an explicit or synthesized constructor, Needlr reports
[NDLRGEN062](analyzers/NDLRGEN062.md) and emits no source.

## XML Documentation

The generated public constructor includes:

- a generated summary;
- primary `<param>` content copied from the positional record's documentation when present;
- marked-property `<param>` content derived from each property's `<summary>`;
- coalesced `<exception>` elements for built-in guards.

Custom guards receive no fabricated exception documentation because their thrown
exceptions cannot be inferred reliably.

## Attribute Reference

### `RecordConstructorOverloadParameterAttribute`

| Setting | Value |
|---|---|
| Target | Property |
| Inherited | No |
| Multiple | No |
| Effect | Adds the property to the record's single generated forwarding overload |

### `ConstructorGuardAttribute`

`ConstructorGuardAttribute` targets fields and properties. On a property it is valid only
when `[RecordConstructorOverloadParameter]` is also present.

## Diagnostics

| ID | Meaning |
|---|---|
| [NDLRGEN057](analyzers/NDLRGEN057.md) | The positional record is not partial |
| [NDLRGEN058](analyzers/NDLRGEN058.md) | The containing type shape is unsupported |
| [NDLRGEN059](analyzers/NDLRGEN059.md) | A marked property is unsupported |
| [NDLRGEN060](analyzers/NDLRGEN060.md) | A property guard has no overload marker |
| [NDLRGEN061](analyzers/NDLRGEN061.md) | Record-overload and field-constructor generation are combined |
| [NDLRGEN062](analyzers/NDLRGEN062.md) | The generated constructor signature would collide |

Guard declarations also use [NDLRGEN047 through NDLRGEN056](analyzers/README.md)
for invalid built-in, custom, aliased, and parameterized guard contracts.

## Runnable Example

Run:

```powershell
dotnet run --project src\Examples\SourceGen\RecordConstructorOverloadExample
```

The example demonstrates a seven-parameter positional record, both construction paths,
the generated null guard, assignment, and the intentional `with`-expression limitation.
