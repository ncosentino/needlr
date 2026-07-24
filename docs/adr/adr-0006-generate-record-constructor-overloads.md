---
title: "ADR-0006: Generate constructor overloads for positional records"
status: "Accepted"
date: "2026-07-23"
authors: ["Nick Cosentino"]
tags: ["architecture", "decision", "source-generation", "records", "constructors"]
supersedes: ""
superseded_by: ""
---

## Context and scope

Positional records provide a concise primary constructor and value-oriented data
model. Some records also need additional stored properties that should be supplied
during construction and guarded before assignment. Adding those properties to the
primary parameter list is not always possible because the primary shape may be a
stable contract shared with serializers, callers, or generated code.

An object initializer can assign the additional properties, but it cannot enforce a
constructor-only guard. A source generator also cannot rewrite the positional primary
constructor, its synthesized properties, the copy constructor, object initializers,
serializer activation, or `with` expressions. It can only add another member to a
partial record.

[ADR-0005](adr-0005-generate-guarded-constructors.md) establishes guarded constructor
generation for eligible fields on ordinary partial classes. That decision deliberately
excludes records and integrates its generated constructor with Needlr dependency
injection and factory metadata. Positional records have different construction
semantics and must remain outside that integration boundary.

This decision governs an additional constructor overload for top-level partial
positional record classes, the property marker that opts properties into that
overload, reuse of constructor guards, and the boundary with Needlr's injectable-type
discovery. It does not redefine record copy semantics, general object validation,
serialization, or mutation after construction.

## Decision drivers

- The positional primary constructor must remain available and unchanged.
- Additional construction-time values and guards must be expressible without
  reflection or hand-written forwarding boilerplate.
- The public trigger must be local to each participating property and unambiguous.
- Generated overload signatures and parameter order must be deterministic.
- Existing constructor guard behavior for fields must remain compatible.
- Invalid record and property shapes must fail at compile time instead of producing
  broken or silently incomplete generated code.
- Nullable annotations should describe the generated overload's effective runtime
  contract without changing CLR signature identity.
- Records must remain excluded from automatic Needlr injection and AOT factory
  metadata.

## Decision

Needlr will provide one property-level marker,
`[RecordConstructorOverloadParameter]`. Applying the marker to at least one eligible
property is the sole positive trigger for this feature. There will be no class-level
record-overload generation attribute.

The feature supports instance properties declared directly by a top-level partial
positional `record class`, including sealed, unsealed, and generic records. Record
structs, body-only records, file-local records, nested records, inherited records,
ordinary classes, positional properties, static properties, indexers, get-only or
required properties, property types less accessible than the generated public
constructor, and other non-assignable properties are outside the contract and produce
compile-time diagnostics.

For each participating record, Needlr will emit exactly one additional public
constructor. Its parameters are:

1. every positional primary-constructor parameter in declaration order; then
2. every marked property in deterministic source order, ordered by source file path
   and source position.

The generated constructor forwards the primary parameters through a `this(...)`
constructor initializer. Its body emits every effective guard for the marked
properties and then assigns those properties. It does not validate or replace the
primary constructor, copy constructor, object initializer, serializer path, or
`with` expression.

Primary parameter types, legal parameter passing modes, tuple shapes, generic type
usage, and escaped identifiers are preserved. A primary `params` parameter is emitted
as its array type because additional property parameters follow it. Optional and
default values are not copied to the generated overload.

`ConstructorGuardAttribute` will support both fields and properties. A guard on a
property is valid only when that property participates through
`[RecordConstructorOverloadParameter]`; otherwise the analyzer reports that the guard
has no record-constructor target. Custom guard aliases may target properties when
their own `AttributeUsage` permits it. Built-in guards, direct custom guards, aliases,
and parameterized aliases normalize through the same resolution and direct-call
emission model established by ADR-0005.

When a marked property's storage type is nullable but its effective built-in guards
reject null (`NotNull`, `NotNullOrEmpty`, or `NotNullOrWhiteSpace`), the generated
parameter uses a nonnullable top-level reference annotation. This communicates the
overload's effective contract to callers. It does not alter CLR signature identity or
the property's storage annotation. Nullable storage without such a built-in guard
remains nullable.

The generator compares the complete proposed parameter sequence against existing
constructors using C# signature identity, ignoring parameter names, optional values,
and nullable annotations. Any collision suppresses generation and produces a
diagnostic. Invalid declarations fail closed rather than emitting partial source.

Generated XML documentation will describe the overload, every forwarded primary
parameter, every added property parameter, and exceptions guaranteed by built-in
guards. Existing positional `<param>` documentation and property summaries are reused
when available, with deterministic generated fallbacks. Custom guards do not create
exception claims that Needlr cannot prove.

This record-only overload is not incorporated into Needlr constructor selection,
injectable discovery, service-catalog metadata, generated factories, or AOT activation
metadata. Records remain excluded from automatic injectable discovery. Combining
`[GenerateConstructor]` with this record-only feature is invalid and produces a
compile-time diagnostic.

## Alternatives considered

### Add a class-level record generation attribute

A class-level attribute could opt in the record while separate annotations select
properties. It was rejected because the property marker already expresses both
participation and intent. A second trigger would create ambiguous partial
configurations and additional rules for records with no selected properties.

### Extend `[GenerateConstructor]` to records

This would reuse an existing public attribute. It was rejected because ADR-0005's
constructor is derived from fields, replaces the implicit class construction path, and
participates in Needlr DI and factory metadata. A positional record overload preserves
an existing primary constructor and deliberately remains outside those integrations.
Combining the two contracts under one attribute would obscure materially different
semantics.

### Require hand-written forwarding constructors

This keeps all behavior in authored source and needs no generator changes. It was
rejected because it repeats the complete primary parameter list, guard clauses, and
assignments, and it is easy for that forwarding signature to drift when the record
changes.

### Use object initializers or `required` properties

Object initializers preserve the primary constructor but cannot guarantee that a guard
runs. `required` members provide compile-time initialization guidance, not runtime
validation, and can still be assigned by serializers or `with` expressions. They do
not satisfy the construction-time guard requirement.

### Rewrite the primary constructor or intercept all property assignment

Rewriting would provide a single validation path, but Roslyn source generators cannot
modify user-authored syntax. Analyzer code fixes or IL weaving would introduce a
different toolchain and maintenance model. Intercepting every assignment would also
change record mutation, serialization, and copy semantics beyond the intended scope.

## Consequences

### Positive

- Positional record contracts remain intact while callers gain one concise guarded
  construction path for additional properties.
- Guard behavior is emitted as ordinary direct C# calls and remains trimming- and
  NativeAOT-safe.
- Property order, overload shape, and documentation are deterministic.
- Existing guard definitions and aliases work across class fields and participating
  record properties without separate runtime mechanisms.
- Nullable annotations communicate built-in null rejection to callers without
  changing runtime signatures.

### Negative

- The generated overload is another public constructor and therefore part of the
  record's compatibility surface.
- Guarded invariants apply only to that overload. Primary construction, object
  initializers, serializers, copy construction, and `with` expressions can still
  produce values that do not satisfy those guards.
- Constructor collision analysis and record-shape diagnostics add compiler-facing
  complexity.
- A property-only trigger means adding or removing one marker can add or remove the
  complete overload.

### Neutral

- The primary constructor remains the canonical positional record constructor.
- Record copy and value-equality semantics are unchanged.
- Needlr's field-based generated constructors retain the DI and factory integration
  established by ADR-0005; record overloads do not acquire that integration.
- Applications that require an invariant across every mutation and deserialization
  path still need a different domain model or validation boundary.

## Confirmation

Generator tests confirm supported record shapes, deterministic parameter order,
preserved parameter syntax, nullable annotation behavior, constructor chaining,
signature collision detection, XML documentation, and incremental caching.

Analyzer tests confirm exact diagnostic IDs, severities, locations, and messages for
unsupported records, unsupported properties, nonparticipating property guards,
guard-resolution failures, constructor collisions, and `[GenerateConstructor]`
misuse.

Executable integration tests confirm both primary and generated construction paths,
exact built-in exception types and parameter names, property assignment, copy behavior,
and the fact that object initializers and `with` expressions remain able to bypass the
overload guard. A runnable example and strict documentation build confirm the public
workflow. Tests also preserve the architectural boundary by confirming that marked
records are not automatically discovered for Needlr injection or factory metadata.

## References

- [ADR-0005: Generate guarded constructors from fields](adr-0005-generate-guarded-constructors.md)
  establishes the sibling field-based constructor feature, its direct guard model, and
  its intentional record exclusion.
- [Generated Constructors](../generated-constructors.md) documents the existing
  field-based feature whose normalized guard behavior is reused.
- [Roslyn incremental generator documentation](https://github.com/dotnet/roslyn/blob/main/docs/features/incremental-generators.md)
  explains the additive source-generation model that prevents rewriting the primary
  constructor.
