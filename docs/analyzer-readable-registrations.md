# Analyzer-readable registrations

Needlr embeds its source-generated registration plan into each participating assembly
as deterministic JSON metadata. Roslyn analyzers and other compile-time tools can read
that plan from a normal PE reference without loading the assembly, evaluating generated
initializers, or reproducing Needlr discovery.

## Quick Start

Reference `NexusLabs.Needlr.Build` or `NexusLabs.Needlr.Generators` normally and enable
source generation:

```xml
<PackageReference Include="NexusLabs.Needlr.Build"
                  Version="x.x.x"
                  PrivateAssets="all" />

<PropertyGroup>
  <NeedlrEmitRegistrationManifest>true</NeedlrEmitRegistrationManifest>
</PropertyGroup>
```

The manifest is disabled by default so applications that do not consume compile-time
registration metadata pay no assembly-size cost. When enabled, the generator adds one
`System.Reflection.AssemblyMetadataAttribute` whose key is
`NexusLabs.Needlr.RegistrationManifest`. Its value is JSON conforming to
`schemas/needlr-registration-manifest-v1.schema.json`.

A Roslyn analyzer can retrieve the payload from an `IAssemblySymbol`:

```csharp
const string manifestKey = "NexusLabs.Needlr.RegistrationManifest";

var manifestJson = assembly.GetAttributes()
    .Where(attribute =>
        attribute.AttributeClass?.ToDisplayString() ==
            "System.Reflection.AssemblyMetadataAttribute" &&
        attribute.ConstructorArguments.Length == 2 &&
        attribute.ConstructorArguments[0].Value is string key &&
        key == manifestKey)
    .Select(attribute => attribute.ConstructorArguments[1].Value as string)
    .SingleOrDefault();
```

The same lookup works when `assembly` comes from a `PortableExecutableReference`.
Consumers must inspect `schemaVersion` before interpreting the remaining fields.

## Contract

The manifest is generated from the same `GeneratedRegistrationPlan` that feeds
Needlr's registration emitters. It is not a second discovery pass.

The root document contains:

| Field | Meaning |
|---|---|
| `schemaVersion` | Compatibility version of the JSON contract |
| `assemblyName` | Assembly that owns this generated registry |
| `isAotProject` | Whether AOT-specific registration emission was selected |
| `serviceCatalogRegistration` | Generated `IServiceCatalog` registration, or `null` for a minimal empty registry |
| `referencedRegistryAssemblies` | Referenced assemblies that own and bootstrap their own generated registries |
| `injectableTypes` | Concrete types, exposed service types, lifetimes, keys, and generated activation parameters |
| `decorators` | Ordered decorator operations |
| `hostedServices` | Concrete hosted-service registrations and activation parameters |
| `interceptedServices` | Implementations, generated proxies, service interfaces, interceptors, and lifetimes |
| `factories` | Generated factory modes, types, and injectable/runtime constructor partitions |
| `providers` | Generated provider service/implementation pairs and property-resolution contracts |
| `options` | Options binding mode, section/name, startup validation, and generated validator registrations |
| `httpClients` | Named client, configuration section, options type, and emitted capability wiring |
| `composedRegistrations` | Closed composition implementation, facade service, lifetime, and activation arguments |
| `plugins` | Bootstrap plugin metadata, kept separate from ordinary DI registrations |

Type identities use Roslyn's fully qualified display form, including the `global::`
prefix. An analyzer comparing a manifest identity with a symbol should use
`SymbolDisplayFormat.FullyQualifiedFormat`.

## Exactness boundary

The manifest describes the plan emitted by Needlr before runtime customization:

- namespace filters, discovery attributes, constructor eligibility, factory handling,
  providers, decorators, composition, and exclusions are already reflected;
- a type absent from the generated plan is absent from the manifest;
- a referenced assembly with its own `[GenerateTypeRegistry]` owns its own manifest and
  is listed under `referencedRegistryAssemblies` instead of being duplicated;
- runtime `ITypeFilterer` exclusions or lifetime overrides, user callbacks, plugin code,
  and registrations from other libraries are not predictable at generation time and
  are outside this contract.

Specialized arrays can intentionally overlap. For example, a hosted or intercepted
type can also appear in `injectableTypes` because those are distinct registration
operations emitted by Needlr.

An assembly carrying `[GenerateTypeRegistry]` receives a manifest only when
`NeedlrEmitRegistrationManifest=true`. A valid manifest whose arrays are empty means the
generator produced a supported empty plan. No manifest means metadata emission was
disabled, the assembly does not participate in Needlr source generation, or it was built
with a version predating this contract.

## Assembly size

The schema is intentionally complete and therefore grows linearly with the generated
registration plan. It repeats type identities and registration details that already
exist elsewhere in generated code, so it is not enabled by default.

Measured Release builds of the same source with and without schema `1.0` metadata:

| Generated entries | JSON payload | DLL increase | PDB increase |
|---:|---:|---:|---:|
| 3 | 1,145 bytes | 1,024 bytes | 720 bytes |
| 5 | 2,125 bytes | 2,048 bytes | 832 bytes |
| 102 | 44,245 bytes | 44,544 bytes | 2,632 bytes |
| 1,002 | 432,145 bytes | 438,784 bytes | 17,500 bytes |

The 1,002-entry fixture measured approximately 438 additional DLL bytes per injectable
service. Consumers should enable the manifest only in assemblies that compile-time tools
actually inspect. The generator test suite fixes an upper budget of 450,000 UTF-8 bytes
for a representative 1,000-service manifest and verifies approximately linear growth.

## Determinism and safety

The payload contains no timestamps, source paths, machine names, user names, or other
ambient state. Unordered string populations use explicit ordinal ordering, semantic
sequences retain their generated order, numbers use invariant formatting, and the
generated source is normalized to LF with every other Needlr output.

Consumers must not load referenced assemblies or execute `IServiceCatalog` to read this
information. Use Roslyn metadata only.

## Attribute Reference

| Attribute | Target | Purpose |
|---|---|---|
| `System.Reflection.AssemblyMetadataAttribute` | Generated assembly | Carries the manifest under the `NexusLabs.Needlr.RegistrationManifest` key |

Needlr does not introduce a user-authored attribute for this feature.

## Configuration

| MSBuild property | Default | Meaning |
|---|---|---|
| `NeedlrEmitRegistrationManifest` | `false` | Emit schema `1.0` metadata into the compiled assembly |

## Analyzers

| Analyzer | Included | Responsibility |
|---|---|---|
| None | No | Needlr publishes registration facts; downstream analyzers own repository-specific policy and diagnostics |

## Schema compatibility

Schema `1.0` is defined by
`schemas/needlr-registration-manifest-v1.schema.json`. Additive fields require a minor
schema version. Removing fields, changing their meaning, or changing registration
identity semantics requires a major schema version. Consumers should reject unsupported
major versions rather than guessing.
