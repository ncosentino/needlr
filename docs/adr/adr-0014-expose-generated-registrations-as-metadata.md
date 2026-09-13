---
title: "ADR-0014: Expose generated registrations as analyzer-readable metadata"
status: "Accepted"
date: "2026-09-12"
authors: ["Nick Cosentino"]
tags: ["architecture", "source-generation", "analyzers", "metadata"]
supersedes: ""
superseded_by: ""
---

## Context and scope

Needlr's source generator discovers types and builds the registration information used
to emit type registries, decorators, hosted services, interceptor proxies, factories,
providers, options, HTTP clients, composed registrations, plugins, and registry
bootstrap code.

The generated `IServiceCatalog` exposes much of that information at runtime. Its
collections are initialized by generated executable code, however, so a Roslyn analyzer
inspecting a normal PE metadata reference can see the catalog's types and property
signatures but cannot evaluate their values.

ADR-0007 established the same compiler boundary for source locations: PE symbols do not
contain generated initializer syntax, and Needlr must not load referenced assemblies or
execute generated catalogs during compilation. That record rejected a metadata protocol
solely for source-location recovery because workspace graph merging already solved that
problem. Registration identity has no equivalent downstream source of truth.

This decision governs a compiler-facing representation of the registration plan emitted
by Needlr. It does not define downstream architectural policy, inspect consumer test
code, or change the runtime dependency-injection behavior.

## Decision drivers

- Compile-time consumers need the exact result of Needlr discovery rather than an
  independently reconstructed approximation.
- The contract must survive ordinary project and package references represented as PE
  metadata.
- Reading the contract must not execute referenced code, load assemblies, parse generated
  C# or IL, or depend on reflection.
- The representation must remain deterministic across builds, cultures, machines, and
  operating systems.
- Assemblies that do not consume the compiler contract must not pay an unbounded
  metadata-size cost.
- Empty generated registries must be distinguishable from assemblies built before the
  contract existed.
- Existing `IServiceCatalog` consumers and runtime registration behavior must remain
  compatible.
- Needlr must expose neutral registration facts without owning repository-specific rules
  built from those facts.

## Decision

Needlr will construct one normalized `GeneratedRegistrationPlan` after discovery and
before source emission. Registration emitters and the analyzer-readable manifest consume
that same plan. Needlr will not run a second classification pass for the manifest.

The manifest is opt-in through `NeedlrEmitRegistrationManifest=true`. Its default is
`false`, so ordinary Needlr applications receive no additional assembly metadata.

An enabled assembly with `[GenerateTypeRegistry]` will receive one generated
`System.Reflection.AssemblyMetadataAttribute`. The key is
`NexusLabs.Needlr.RegistrationManifest`; the value is deterministic JSON conforming to
`schemas/needlr-registration-manifest-v1.schema.json`.

The manifest records:

- the owning assembly and whether AOT-specific emission was selected;
- the generated service-catalog registration when present;
- referenced assemblies that own separate generated registries;
- injectable types, exposed service types, lifetimes, keys, and activation parameters;
- decorators, hosted services, intercepted services and proxies, generated factories,
  providers, options, HTTP clients, and composed registrations;
- plugin metadata in a separate collection because plugin bootstrap is not an ordinary
  DI service registration.

Type identities use Roslyn's fully qualified display representation. Source paths and
other machine-specific values are excluded. Collections that do not carry an execution
order are serialized with explicit ordinal ordering; semantically ordered values retain
their generated order.

The metadata describes Needlr's generated plan before runtime customization.
`ITypeFilterer` overrides, user callbacks, plugin-executed registrations, and
registrations from other libraries remain outside the compile-time contract.

An enabled empty participant emits schema `1.0` with empty collections and no
service-catalog registration. Absence of the metadata attribute means emission was
disabled, the assembly is not a manifest-bearing Needlr participant, or it predates the
contract; consumers must not silently reconstruct an authoritative result from naming
conventions.

The metadata key and JSON schema are compatibility contracts. Additive schema changes
increment the minor version. Removing fields or changing their meaning requires a new
major version.

Needlr will not ship a diagnostic that interprets this metadata as a requirement to
resolve services from a container. Downstream analyzers own their diagnostics,
activation conditions, severities, exemptions, and repository policy.

Release measurements show why emission is opt-in. A representative 102-entry manifest
added 44,544 bytes to its DLL; a 1,002-entry manifest added 438,784 bytes, approximately
438 bytes per injectable service. The test suite enforces a 450,000-byte ceiling for the
representative 1,000-service payload and verifies approximately linear growth.

## Alternatives considered

### Add a Needlr analyzer for direct construction

A built-in analyzer could inspect object creation in test projects. It was rejected
because whether direct construction is allowed is a consumer testing and architecture
policy. Legitimate unit tests often construct their subject directly, and Needlr should
not own test detection or consumer-specific exemptions.

### Recompute discovery in each downstream analyzer

An analyzer could copy or share portions of Needlr's symbol-discovery logic. It was
rejected because it would run against a different compilation and potentially a
different Needlr version. Namespace filters, generated constructors, factories,
providers, decorators, and future rules could drift from the registration plan that
actually produced the referenced assembly.

### Parse generated source or method bodies

Generated syntax may be available through a `CompilationReference`, but normal builds
use PE references without declaring syntax. Parsing IL would couple consumers to code
shape rather than semantics and require file access. Both approaches were rejected.

### Load and invoke `IServiceCatalog`

Executing a referenced assembly would recover runtime values, but analyzers must not load
or run arbitrary build inputs. Dependency loading, side effects, architecture mismatch,
and build-host safety make this unacceptable.

### Publish one public attribute type per registration kind

Typed attributes would expose Roslyn type symbols directly. They were rejected because
the growing set of registration kinds would create a broad runtime public API that is
harder to evolve than a versioned data schema. A single BCL assembly-metadata attribute
keeps the compatibility surface explicit and transport-neutral.

### Emit the JSON manifest by default

Always-on emission would make the contract immediately available to every downstream
tool. It was rejected after Release measurements showed 44,544 bytes of additional DLL
size for 102 entries and 438,784 bytes for 1,002 entries. Consumers that do not use the
metadata should not pay that cost.

### Write a sidecar file

A JSON file in the producer's intermediate output could avoid assembly metadata. It was
rejected because additional files do not naturally propagate across project references
or NuGet packages, IDE builds may not share the same output state, and path coordination
would reintroduce machine-specific coupling.

## Consequences

### Positive

- Downstream analyzers can consume the exact source-generated plan from ordinary PE
  references.
- Registration policy remains separate from Needlr's neutral compiler contract.
- Existing discovery behavior and runtime catalogs remain unchanged.
- Empty manifests and schema versions let consumers distinguish supported absence from
  unavailable metadata.
- The contract is deterministic, reflection-free, and compatible with Native AOT build
  workflows.

### Negative

- Needlr assumes long-term compatibility responsibility for another versioned schema.
- Enabled assemblies gain a JSON metadata payload proportional to their generated
  registration plan.
- Type identities are serialized strings; consumers must compare them using the
  documented Roslyn display format rather than receiving direct `ITypeSymbol` attribute
  arguments.
- Changes to registration semantics now require coordinated updates to generation,
  metadata, schema, and parity tests.

### Neutral

- Specialized registration collections can overlap with `injectableTypes` because they
  represent distinct generated operations.
- Older assemblies remain readable as ordinary references but do not retroactively gain
  a manifest.
- Disabled assemblies have zero manifest payload and remain indistinguishable from older
  or nonparticipating assemblies to downstream tools.
- Runtime filters and callbacks can still change the final `IServiceCollection`; the
  manifest intentionally describes generated input, not post-runtime state.

## Confirmation

Generator tests compile representative services, factories, decorators, hosted and
intercepted services, providers, options, HTTP clients, composed registrations, filtered
types, and cross-assembly registries. They assert that the manifest contains the same
discovered values used by source emission and no excluded values.

A producer compilation is emitted to a PE image and referenced by a second Roslyn
compilation. The test reads the assembly metadata and parses the manifest without source
syntax, file access, reflection, or assembly loading.

Determinism tests cover the manifest with every other generated file under hostile
cultures and LF normalization. The committed schema and manifest shape tests keep the
public metadata contract synchronized.

Size-budget tests serialize representative 100- and 1,000-service plans, enforce an
upper bound for the latter, and reject superlinear growth. Package tests verify that both
direct generator consumption and `NexusLabs.Needlr.Build` expose the opt-in MSBuild
property to the compiler.

## References

- `TypeRegistryGenerator` constructs the discovery result and dispatches every generated
  registration surface, demonstrating why one normalized plan must sit at that boundary.
- `GeneratedTypeRegistrar` expands generated injectable metadata into self, interface,
  keyed, and lifetime-specific runtime registrations.
- `ServiceCatalogCodeGenerator` demonstrates the existing runtime introspection surface
  whose initializer values are unavailable through PE symbols.
- ADR-0007 documents why generated initializer syntax and runtime execution are not valid
  cross-assembly compiler transports.
- `schemas/needlr-registration-manifest-v1.schema.json` defines the compatibility
  contract consumed by external tooling.
