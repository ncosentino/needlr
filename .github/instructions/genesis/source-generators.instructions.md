---
applyTo: "**/*Generator.cs,**/*Generator.*.cs"
---

# Source Generator Rules

## Class shape

- Decorate with `[Generator(LanguageNames.CSharp)]`, implement `IIncrementalGenerator`
- Always `public sealed class` (or `internal sealed class` if the generator is not shipped as a public API)

```csharp
[Generator(LanguageNames.CSharp)]
public sealed class MyFeatureGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // pipeline setup here
    }
}
```

## netstandard2.0 constraints

Source generator projects MUST target `netstandard2.0`. This means:

- No `record` types
- No `init`-only property setters
- No `ImplicitUsings`
- No C# 9+ runtime features (but `LangVersion=latest` is set, so syntax features backed by the compiler work)

## Generator project shape

Generator `.csproj` files must include:

```xml
<TargetFramework>netstandard2.0</TargetFramework>
<IsRoslynComponent>true</IsRoslynComponent>
<EnforceExtendedAnalyzerRules>true</EnforceExtendedAnalyzerRules>
```

## Consuming a local source generator

Projects that consume a source generator from within the same solution reference it as:

```xml
<ProjectReference Include="..\MyApp.Generators\MyApp.Generators.csproj"
  OutputItemType="Analyzer"
  ReferenceOutputAssembly="false" />
```

`OutputItemType="Analyzer"` tells MSBuild to treat the output as an analyzer/generator. `ReferenceOutputAssembly="false"` prevents the generator's assembly from being included in the consumer's runtime dependencies.

## Structural discipline

The generator file contains ONLY the `Initialize` method and a top-level orchestration method. All other logic is separated:

| Concern | Location | Shape |
|---------|----------|-------|
| Generator entry point | `*Generator.cs` | `Initialize` + orchestration only |
| Code emission | Dedicated static classes | `internal static class` |
| Attribute/symbol discovery | `*DiscoveryHelper.cs` or `*AttributeHelper.cs` | `internal static class` |
| Discovery result models | Dedicated model files | `internal readonly struct`, one per file |

**NEVER put emission logic inline in the generator.** Delegate to dedicated emission classes.

**NEVER put discovery logic inline.** Delegate to dedicated helper classes.

## Emission rules

### Class shape

- Emission classes are `internal static class`
- Entry-point methods called by the generator: `internal static`
- Helper methods used only within the file: `private static`

### Generated code targets the consumer

The generated C# must compile on the **consumer's** target framework (e.g., `net10.0`), which differs from the generator's `netstandard2.0`. Use `global::` prefixes for all type references in emitted code to avoid namespace collisions:

```csharp
builder.AppendLine("        global::System.Collections.Generic.List<string> items = new();");
```

### StringBuilder pattern

Use `StringBuilder` with manual indentation (4 spaces per level) via `builder.AppendLine(...)`. Keep indentation consistent across all emission methods.

### Capability-conditional emission

When emitting code for a feature with optional capabilities:

- Emit a wiring block ONLY when the discovered type actually implements the capability
- NEVER emit dead/stub wiring for capabilities not opted into
- Use bit-flag enums or boolean fields on discovery models to pass capability detection results from discovery to emission

This is the load-bearing extensibility pattern: future capabilities ship as new interfaces + new conditional emission blocks, with zero impact on existing consumers.
