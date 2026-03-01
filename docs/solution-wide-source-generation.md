# Solution-Wide Source Generation

When you have multiple projects in a solution and want Needlr source generation to work consistently across all of them, the recommended approach is to use the `NexusLabs.Needlr.Build` MSBuild package.

## The Problem Without It

Without a centralized setup, each project needs to individually reference `NexusLabs.Needlr.Generators` as an analyzer and manage the `NeedlrAutoGenerate` property. In large solutions this leads to:

- Duplication across every `.csproj`
- Easy mistakes (missing generator reference, wrong property name)
- Confusion when integration packages also reference the generator (see below)

## Recommended Setup

Add one package reference to your solution-level `Directory.Build.props`:

```xml
<Project>
  <ItemGroup>
    <PackageReference Include="NexusLabs.Needlr.Build" Version="x.x.x" PrivateAssets="all" />
  </ItemGroup>

  <PropertyGroup>
    <!-- Enable generation for every project; individual projects can override to false -->
    <NeedlrAutoGenerate>true</NeedlrAutoGenerate>
  </PropertyGroup>
</Project>
```

That's it. The package handles:

- Making `NexusLabs.Needlr.Generators` and `NexusLabs.Needlr.Generators.Attributes` available to every project
- Activating the generator only for projects where `NeedlrAutoGenerate=true`
- Deduplicating the generator DLL if it appears more than once in the analyzer list (defensive, see below)

## Opting Out in Test Projects

Test helper libraries and shared test infrastructure typically don't need their own `TypeRegistry`. Override the property in the project file:

```xml
<!-- MyFeature.Tests.csproj -->
<PropertyGroup>
  <NeedlrAutoGenerate>false</NeedlrAutoGenerate>
</PropertyGroup>
```

The generator DLL is still in the NuGet graph (so attribute types compile), but it won't run and produce output for that project.

## Why the Generator Can Appear Twice (and How We Handle It)

If your project references both `NexusLabs.Needlr.Build` and an integration package like `NexusLabs.Needlr.AgentFramework` or `NexusLabs.Needlr.SemanticKernel`, Roslyn could theoretically see two copies of `NexusLabs.Needlr.Generators.dll` — one from each package path. Running the generator twice produces duplicate type registration code that fails to compile.

`NexusLabs.Needlr.Build` ships a `DeduplicateNeedlrGeneratorAnalyzers` MSBuild target that runs before compilation and ensures only one copy of the generator DLL is passed to Roslyn, regardless of how many are present in the analyzer item group. The integration packages (`AgentFramework`, `SemanticKernel`, `SignalR`) ship the same target as defense-in-depth.

## Multi-Assembly TypeRegistry Composition

When multiple projects in your solution each have `[assembly: GenerateTypeRegistry]`, Needlr generates a `TypeRegistry` in each of them. At runtime, each assembly's `[ModuleInitializer]` calls `NeedlrSourceGenBootstrap.Register()`, which accumulates all registered providers.

When you call `new Syringe().UsingSourceGen()`, it calls `TryGetProviders()`, which combines and deduplicates all registered providers from all loaded assemblies. This means:

- An entry-point project referencing a Bootstrap project (which references feature projects) gets all types from all of them
- Test projects that reference any of those get full service resolution without needing their own TypeRegistry

## Example: MultiProjectApp

The `src/Examples/MultiProjectApp/` example in this repository demonstrates this pattern end-to-end:

- Feature projects (`Notifications`, `Reporting`) each have their own TypeRegistry
- `Bootstrap` references both feature projects, acting as the single "pull everything in" anchor
- Entry points (`ConsoleApp`, `WorkerApp`) reference only Bootstrap
- Test projects set `NeedlrAutoGenerate=false` and consume the assembled TypeRegistry from the projects they reference

See the [example README](https://github.com/ncosentino/needlr/tree/main/src/Examples/MultiProjectApp) for the full structure.
