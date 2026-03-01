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
└── MultiProjectApp.Features.Reporting.Tests/← feature unit tests (source gen opted out)
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

### Bootstrap Pattern

`MultiProjectApp.Bootstrap` exists solely to pull all feature projects into the build graph. Both entry points reference only Bootstrap — not the individual feature projects. This means adding a new feature project only requires updating Bootstrap.

### `[DoNotAutoRegister]` on a Plugin Class

`BootstrapPlugin` in `MultiProjectApp.Bootstrap` has `[DoNotAutoRegister]` applied directly. This demonstrates that the attribute does **not** suppress plugin discovery — it only affects DI auto-registration of the class itself as a service. The plugin's `Configure` method is still called during startup.

## Running the Example

```sh
# Run the console app
dotnet run --project MultiProjectApp.ConsoleApp

# Run the worker app
dotnet run --project MultiProjectApp.WorkerApp

# Run all tests
dotnet test
```
