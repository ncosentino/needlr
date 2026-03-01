# MultiProjectApp Example

Demonstrates how to set up Needlr source generation across a multi-project solution — a common real-world pattern where feature functionality lives in separate class libraries and one or more entry points compose them together.

## Project Structure

```
MultiProjectApp/
├── Directory.Build.props                    ← enables Needlr for the whole example
├── MultiProjectApp.Features.Notifications/  ← feature: notification service
├── MultiProjectApp.Features.Reporting/      ← feature: report service
├── MultiProjectApp.Bootstrap/               ← anchor: imports all feature plugins
├── MultiProjectApp.ConsoleApp/              ← entry point: console app
├── MultiProjectApp.WorkerApp/               ← entry point: hosted service / background worker
├── MultiProjectApp.ConsoleApp.Tests/        ← integration tests (source gen opted out)
├── MultiProjectApp.Features.Reporting.Tests/← feature unit tests (source gen opted out)
└── MultiProjectApp.Integration.Tests/       ← integration tests (source gen enabled — test types discovered)
```

## Key Patterns

### Solution-Wide Source Generation (`Directory.Build.props`)

A single `Directory.Build.props` at the solution root enables Needlr for every project:

```xml
<Project>
  <!-- In a real solution, reference NexusLabs.Needlr.Build instead of importing the props directly -->
  <Import Project="path/to/NexusLabs.Needlr.Generators.props" />
  <PropertyGroup>
    <NeedlrAutoGenerate>true</NeedlrAutoGenerate>
  </PropertyGroup>
</Project>
```

In production, use the `NexusLabs.Needlr.Build` NuGet package instead:

```xml
<Project>
  <ItemGroup>
    <PackageReference Include="NexusLabs.Needlr.Build" Version="..." PrivateAssets="all" />
  </ItemGroup>
  <PropertyGroup>
    <NeedlrAutoGenerate>true</NeedlrAutoGenerate>
  </PropertyGroup>
</Project>
```

### Opting Out in Test Projects

Test projects that don't need a TypeRegistry override `NeedlrAutoGenerate` in their `.csproj`:

```xml
<PropertyGroup>
  <NeedlrAutoGenerate>false</NeedlrAutoGenerate>
</PropertyGroup>
```

This prevents the generator from running in those projects while still allowing them to consume TypeRegistries from the projects they reference (via module initializers).

### Keeping Source Generation Enabled in Test Projects

`MultiProjectApp.Integration.Tests` shows the opposite pattern: source generation is intentionally left **enabled** so that test-only plugin types (like `TestInfrastructurePlugin`) are discovered automatically alongside the real feature plugins.

> **Important:** Generator references are *not* transitively propagated as Roslyn Analyzer items. A test project that keeps source gen enabled must explicitly reference `NexusLabs.Needlr.Generators.Attributes` and add `NexusLabs.Needlr.Generators` with `OutputItemType="Analyzer"`, just like any non-test project:
>
> ```xml
> <ProjectReference Include="path/to/NexusLabs.Needlr.Generators.Attributes.csproj" />
> <ProjectReference Include="path/to/NexusLabs.Needlr.Generators.csproj"
>                   OutputItemType="Analyzer"
>                   ReferenceOutputAssembly="false" />
> ```

A `[assembly: GenerateTypeRegistry(IncludeNamespacePrefixes = ["MyTest.Assembly"])]` attribute in the test project scopes type discovery to that assembly, avoiding unintended pickup of types from referenced feature assemblies (which have their own TypeRegistries already).

### Bootstrap Pattern

`MultiProjectApp.Bootstrap` exists solely to pull all feature projects into the build graph. Both entry points reference only Bootstrap — not the individual feature projects. This means adding a new feature project only requires updating Bootstrap.

## Running the Example

```sh
# Run the console app
dotnet run --project MultiProjectApp.ConsoleApp

# Run the worker app
dotnet run --project MultiProjectApp.WorkerApp

# Run all tests
dotnet test
```
