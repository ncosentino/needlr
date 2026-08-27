---
applyTo: "**/*.csproj,**/Directory.Packages.props,**/Directory.Build.props,**/*.slnx"
---

# Needlr Project Files

## Central package management

All NuGet package versions are declared in `src/Directory.Packages.props`
(`ManagePackageVersionsCentrally=true`). Individual `.csproj` files reference packages by
name only:

```xml
<!-- CORRECT -->
<PackageReference Include="Microsoft.Extensions.Logging" />

<!-- WRONG — inline versions bypass central management -->
<PackageReference Include="Microsoft.Extensions.Logging" Version="10.0.3" />
```

Adding a package means two edits: a `<PackageVersion>` entry in
`src/Directory.Packages.props` and a `<PackageReference>` in the consuming project.

## Analyzer and generator packages

Reference build-time-only packages with `PrivateAssets="all"` so they do not flow to
consumers of the produced NuGet package.

## Determinism

`src/Directory.Build.props` sets `<Deterministic>true</Deterministic>` and enables
`ContinuousIntegrationBuild` under CI. Do not disable either. That flag only constrains the
compiler — see `generator-determinism.instructions.md` for the rules that keep generated
source deterministic.
