---
# AUTO-GENERATED from .github/instructions/integration-tests.instructions.md — do not edit
paths:
  - "**/IntegrationTests/**/*.cs"
---
# Integration Test Rules

## Framework

xUnit v3 with `[Fact]` attributes. No `[Theory]` unless data-driven variation is genuinely needed.

## Service provider setup

Build the provider via the Syringe fluent API:

```csharp
private static IServiceProvider BuildProvider(IConfiguration configuration)
{
    return new Syringe()
        .UsingGeneratedComponents(
            NexusLabs.Needlr.IntegrationTests.Generated.TypeRegistry.GetInjectableTypes,
            NexusLabs.Needlr.IntegrationTests.Generated.TypeRegistry.GetPluginTypes)
        .BuildServiceProvider(configuration);
}
```

## Configuration

Always use in-memory configuration — never file-based in tests:

```csharp
var configuration = new ConfigurationBuilder()
    .AddInMemoryCollection(new Dictionary<string, string?>
    {
        ["Section:Key"] = "Value",
    })
    .Build();
```

## Test types (options records, helper services)

Needlr integration-test fixtures override the general top-level type-isolation default:
a fixture owned by one test stays beside that test so source-generation discovery and
the assertion remain visible together. Co-locate test-only options records and helper
services after the test class; extract one only when another test file reuses it.

## Assembly-level attribute

`[assembly: GenerateTypeRegistry]` lives in `GeneratorAssemblyInfo.cs`. Do NOT create a manual one if the Needlr `.targets` file auto-generates it for the project (check for `NeedlrGeneratedTypeRegistry.g.cs` in `obj/`).
