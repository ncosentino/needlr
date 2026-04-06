---
applyTo: "**/Examples/**/*.cs,**/Examples/**/*.csproj,**/Examples/**/*.json"
---

# Example Project Rules

## Target framework

All example projects target `net10.0`.

## Generator wiring

Reference the generator via project reference:

```xml
<ProjectReference Include="..\..\NexusLabs.Needlr.Generators\NexusLabs.Needlr.Generators.csproj"
  OutputItemType="Analyzer"
  ReferenceOutputAssembly="false" />
```

Do NOT create a manual `GeneratorAssemblyInfo.cs` — the Needlr `.targets` file auto-emits `[assembly: GenerateTypeRegistry]` based on the assembly name.

## Configuration files

`appsettings.json` must be marked for copy:

```xml
<None Update="appsettings.json">
    <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
</None>
```

## Solution registration

Register new example projects in `src/NexusLabs.Needlr.slnx` under the appropriate `/Examples/` folder.

## Self-documenting output

Example `Program.cs` should print verbose console output explaining what the example demonstrates. Each feature shown should be labeled so a reader running the example understands what they're seeing without reading the source.
