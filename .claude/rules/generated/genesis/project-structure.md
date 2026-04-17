---
# AUTO-GENERATED from .github/instructions/genesis/project-structure.instructions.md — do not edit
paths:
  - "**/*.csproj"
---
# Project File Rules

## Feature Projects in Bootstrap

- Feature projects that are supposed to be part of BrandGhost must be referenced in the Bootstrap project.
- Feature test projects do NOT get included in bootstrap

## SLNX

- You must include the feature project in the SLNX
- The feature project folder exists at the repo root, but within the solution file's "Feature" section
- The feature's test project folder is a sibling, and the reference in the SLNX is also a sibling

## Cross Feature References

- It is STRICTLY prohibited to reference another feature project across feature boundaries. You must route such concerns via the SDK project.
- A project that is a specific implementation of a feature can reference the feature project that is the namespace level above it. So MyProduct.Features.TheFeatureArea.TheFeatureImplementation can reference MyProduct.Features.TheFeatureArea.

## `InternalsVisibleTo` for testable projects

Every feature project that has (or may have) tests MUST declare `InternalsVisibleTo` using the `<AssemblyAttribute>` pattern directly in the `.csproj` file. Do NOT use `AssemblyInfo.cs`.

Add the following `ItemGroup` to every feature project's `.csproj`:

```xml
<ItemGroup>
  <AssemblyAttribute Include="System.Runtime.CompilerServices.InternalsVisibleTo">
    <_Parameter1>$(AssemblyName).Tests</_Parameter1>
  </AssemblyAttribute>
  <AssemblyAttribute Include="System.Runtime.CompilerServices.InternalsVisibleTo">
    <_Parameter1>$(AssemblyName).Tests.Unit</_Parameter1>
  </AssemblyAttribute>
  <AssemblyAttribute Include="System.Runtime.CompilerServices.InternalsVisibleTo">
    <_Parameter1>$(AssemblyName).Tests.Functional</_Parameter1>
  </AssemblyAttribute>
  <AssemblyAttribute Include="System.Runtime.CompilerServices.InternalsVisibleTo">
    <_Parameter1>NexusAI.Tests.Functional</_Parameter1>
  </AssemblyAttribute>
  <AssemblyAttribute Include="System.Runtime.CompilerServices.InternalsVisibleTo">
    <_Parameter1>DynamicProxyGenAssembly2, PublicKey=0024000004800000940000000602000000240000525341310004000001000100c547cac37abd99c8db225ef2f6c8a3602f3b3606cc9891605d02baa56104f4cfc0734aa39b93bf7852f7d9266654753cc297e7d2edfe0bac1cdcf9f717241550e0a7b191195b7667bb4f64bcb8e2121380fd1d9d46ad2d92d2d15605093924cceaf74c4861eff62abf69b9291ed0a340e113be11e6a7d3113e92484cf7045cc7</_Parameter1>
  </AssemblyAttribute>
</ItemGroup>
```

The `DynamicProxyGenAssembly2` entry is required for Moq to proxy `internal` types. Without it, `MockBehavior.Strict` mocks of internal interfaces will fail at runtime.

### Why this pattern

Using `<AssemblyAttribute>` in `.csproj` instead of `AssemblyInfo.cs` keeps the project file as the single source of truth and avoids conflicts with implicit using generation. All existing feature projects in this repository follow this pattern.

## Central package management

`Directory.Packages.props` declares all package versions with `ManagePackageVersionsCentrally=true`. Individual `.csproj` files reference packages by name only — never specify a version inline:

```xml
<!-- Directory.Packages.props — single source of truth for versions -->
<PackageVersion Include="Serilog" Version="4.2.0" />

<!-- Feature.csproj — name only, no version -->
<PackageReference Include="Serilog" />
```

When adding a new package:

1. Add the `<PackageVersion>` entry to `Directory.Packages.props` first
2. Then add the `<PackageReference>` (name only) to the consuming `.csproj`

Never use `Version=` on a `<PackageReference>` — it will conflict with central management and produce build warnings or errors.
