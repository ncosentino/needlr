---
title: "ADR-0005: Generate guarded constructors from fields"
status: "Accepted"
date: "2026-07-21"
authors: ["Nick Cosentino"]
tags: ["architecture", "decision", "source-generation", "dependency-injection", "constructors"]
supersedes: ""
superseded_by: ""
---

## Context and scope

C# primary constructors reduce constructor syntax when parameters can be used
directly by the type. They do not provide a constructor body in which developers
can place guard clauses. Enforcing runtime invariants therefore requires explicit
field or property initializers that repeat the parameter, storage member, and
validation expression. The repetition grows with every dependency and removes much
of the concision primary constructors provide.

Roslyn source generators are additive. They can contribute members to partial
types, but they cannot rewrite a user-authored primary constructor or inject
statements into it. Needlr can therefore remove this boilerplate only by generating
a complete constructor from declarations that remain in user source.

Needlr already emits compile-time factories that call discovered constructors
directly. Generators in the same compilation cannot consume one another's output,
so a constructor generated independently of the type registry would be invisible
when Needlr computes its AOT factory. Needlr's `DeferToContainerAttribute` exists
for this limitation when external generators add constructors. A native feature
must solve the limitation without asking consumers to repeat generated parameter
types.

This decision governs constructor generation for partial service classes, guard
extension points, and integration with Needlr's generated registration metadata.
It does not define general object validation, mutate primary constructors, or
permit arbitrary source expressions in attributes.

## Decision drivers

- Common constructor and guard code should be removable without hiding dependency
  requirements.
- Generated constructors must remain ordinary C# constructors that work for direct
  callers and any dependency-injection container.
- Invalid guard declarations must fail at compile time with targeted diagnostics.
- Generated registration and Native AOT factories must use the same effective
  constructor shape.
- Custom application invariants must be extensible without runtime reflection.
- The default behavior must not add new runtime exceptions unless guards are
  explicitly requested.
- Generated output must be deterministic, documented, and regression tested through
  executable behavior rather than source snapshots alone.

## Decision

Needlr will provide opt-in source generation of one public constructor for an
eligible partial class. The constructor parameters and assignments are derived from
eligible private instance `readonly` fields.

`[GenerateConstructor]` generates assignments without guards. A class-level guard
mode can explicitly enable automatic guards for non-nullable reference fields.
Positive field-level constructor guard attributes also enable generation when the
class attribute is absent. In that case, generation uses the no-default-guards mode:
all eligible fields become parameters, while only explicitly annotated fields are
guarded. Exclusion-only attributes do not enable generation.

Needlr will provide a closed set of built-in guard kinds for unambiguous common
contracts, initially null, null-or-empty, and null-or-whitespace validation. Analyzer
diagnostics will reject guard kinds that are incompatible with the target field.

Custom guards will be selected by type and, when necessary, a compile-time-validated
method name. The generator will resolve an accessible compatible static validation
method and emit a direct call. Applications may define domain-specific field
attributes by decorating their attribute type with a Needlr guard-definition
meta-attribute. Direct guards and custom aliases will normalize into the same
internal model. Needlr will not invoke guards through reflection or splice arbitrary
C# expressions supplied as strings.

Constructor source generation, type-registry discovery, AOT factory emission,
service-catalog metadata, and constructor-based analyses will consume one shared
discovered-constructor model. The generated constructor will not require
`[DeferToContainer]`.

The feature will initially reject existing explicit instance constructors and base
types that require constructor arguments. This preserves one unambiguous generated
constructor while the contract is established.

## Alternatives considered

### Continue using hand-authored or primary constructors

This requires no new Needlr surface and keeps all validation visible in user source.
It was rejected because guarded primary constructors repeat storage declarations and
assignment expressions, while traditional constructors repeat the entire parameter,
guard, and assignment sequence.

### Recommend or depend on AutoCtor or AutoConstructor

Both libraries generate constructors from fields and demonstrate that the model is
viable. Depending on one would reduce Needlr implementation work. It was rejected as
the architectural direction because their guard contracts are primarily global
null-check switches, they do not share Needlr's effective constructor metadata, and
Needlr would still need integration logic to keep its generated AOT factories
correct. A transitive analyzer dependency would also make Needlr's behavior depend
on another generator's release and diagnostic policies.

### Annotate primary-constructor parameters

Parameter annotations would preserve primary-constructor syntax. A source generator
cannot inject the corresponding executable statements into that constructor,
however. An analyzer code fix could rewrite the user's file, but the resulting guard
code would become hand-maintained source and would not stay synchronized with the
annotations. This remains a possible editor convenience, not the generation model.

### Generate constructors implicitly for every registered type

This would minimize annotations but would couple registration to class authoring and
could silently change constructor selection for existing types. It was rejected
because constructor generation must be an explicit declaration of intent and must
not alter unrelated Needlr registrations.

### Accept arbitrary initializer or guard expressions as strings

This permits unrestricted customization with a small attribute surface. It was
rejected because renames and type checking would not apply to the expression,
failures would surface as generated-code compiler errors, and safe semantic analysis
would be impractical. Typed guard kinds and direct method references provide an
extensible compile-time contract instead.

## Consequences

### Positive

- Service classes can declare dependencies once as fields while Needlr supplies the
  constructor, guards, assignments, and XML documentation.
- Guard behavior is explicit and applies to direct construction as well as DI
  activation.
- Custom guards remain direct, trim-safe calls with analyzer-validated signatures.
- Needlr's generated factories and catalogs remain accurate because they share the
  constructor model used for emission.
- The feature creates no runtime reflection dependency.

### Negative

- Constructor order becomes generated public API and requires deterministic field
  ordering and compatibility discipline.
- A field-level guard can cause a constructor to be generated for all eligible fields
  in the containing class, which must be clearly documented.
- Existing constructors, inheritance, properties, records, and advanced
  initialization hooks require diagnostics or later design work.
- Needlr assumes ownership of constructor-generation edge cases and diagnostics that
  external libraries already maintain.
- Custom guard authors must follow a static method contract rather than implementing
  arbitrary validation syntax.

### Neutral

- Primary constructors remain appropriate when no runtime guards or explicit storage
  members are required.
- `DeferToContainerAttribute` remains the interoperability mechanism for constructors
  emitted by external generators.
- Constructor guards throw exceptions because constructors cannot return a result
  value; the feature does not establish exception-based validation for other APIs.

## Confirmation

Compliance is confirmed by red-to-green behavioral tests that compile and execute
generated constructors, assert exact exception types and parameter names, and resolve
the generated types through a real Needlr `Syringe`. Tests must also prove that AOT
factories and service-catalog metadata use the generated parameter model and never
emit an incorrect parameterless activation.

Analyzer tests confirm exact diagnostic IDs, locations, severities, and messages for
invalid class shapes, field eligibility, guard compatibility, and custom guard
methods. Executable examples and strict documentation validation confirm that the
public workflow and extension contract remain usable.

## References

- `DeferToContainerAttribute` documents Needlr's existing interoperability contract
  for constructors emitted by an external source generator and explains why generated
  outputs cannot be discovered by a peer generator in the same compilation.
- Needlr's type registry and injectable-type code generator demonstrate that
  constructor parameters are compiled into direct `GetRequiredService<T>()` factory
  calls for reflection-free activation.
- [Roslyn source generator documentation](https://github.com/dotnet/roslyn/blob/main/docs/features/incremental-generators.md)
  defines generators as additive compilation contributors rather than source
  rewriters.
- [AutoCtor](https://github.com/distantcam/AutoCtor) and
  [AutoConstructor](https://github.com/k94ll13nn3/AutoConstructor) provide prior art
  for generating constructors from fields.
