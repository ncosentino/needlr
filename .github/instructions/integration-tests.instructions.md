---
applyTo: "**/IntegrationTests/**/*.cs"
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

Co-locate test options records and helper service classes in the SAME file as the test class, after the closing brace. Do NOT put them in a separate `TestTypes/` file. This is the established house style.

## Assembly-level attribute

`[assembly: GenerateTypeRegistry]` lives in `GeneratorAssemblyInfo.cs`. Do NOT create a manual one if the Needlr `.targets` file auto-generates it for the project (check for `NeedlrGeneratedTypeRegistry.g.cs` in `obj/`).
