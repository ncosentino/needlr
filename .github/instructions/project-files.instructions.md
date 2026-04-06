---
applyTo: "**/*.csproj,**/*.slnx,**/Directory.Packages.props,**/Directory.Build.props"
---

# Project File Rules

## Central package management

`src/Directory.Packages.props` declares all package versions (`ManagePackageVersionsCentrally=true`). Individual `.csproj` files reference packages by name only — never specify a version inline.

When adding a new package: add the version to `Directory.Packages.props` first, then reference it from the `.csproj`.

## Generator project references

Projects that consume a source generator reference it as:

```xml
<ProjectReference Include="..\NexusLabs.Needlr.Generators\NexusLabs.Needlr.Generators.csproj"
  OutputItemType="Analyzer"
  ReferenceOutputAssembly="false" />
```

## Generator project shape

Generator `.csproj` files must include:

```xml
<TargetFramework>netstandard2.0</TargetFramework>
<IsRoslynComponent>true</IsRoslynComponent>
<EnforceExtendedAnalyzerRules>true</EnforceExtendedAnalyzerRules>
```

## Solution file

The solution uses `.slnx` format (XML-based). Projects are organized under `<Folder>` elements:

- `/Examples/SourceGen/` — source-gen example apps
- `/Examples/Reflection/` — reflection example apps
- `/Tests/` — test projects
- `/Integrations/` — framework integration projects

## Test projects

New test projects need these package references (all versioned centrally):

- `xunit.v3`
- `Microsoft.NET.Test.Sdk`
- `xunit.runner.visualstudio`
- `coverlet.collector`
