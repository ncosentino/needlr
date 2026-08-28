---
# AUTO-GENERATED from .github/instructions/discovery-helpers.instructions.md — do not edit
paths:
  - "**/*DiscoveryHelper.cs"
  - "**/*AttributeHelper.cs"
---
# Discovery Helper Rules

## Class shape

`internal static class` with no instance state. All methods are either `internal static` (entry points) or `private static` (helpers).

## Attribute detection

Match attributes by BOTH simple name and containing namespace:

```csharp
// CORRECT
attributeClass.Name == "OptionsAttribute" &&
attributeClass.ContainingNamespace?.ToDisplayString() == "NexusLabs.Needlr.Generators"

// WRONG — full metadata name can be unreliable across assemblies
attributeClass.ToDisplayString() == "NexusLabs.Needlr.OptionsAttribute"
```

## Extracting attribute data

- Positional arguments: `attribute.ConstructorArguments[0].Value`
- Named arguments: iterate `attribute.NamedArguments`, match by `Key`
- Always null-check and cast safely

## Return types

Return `readonly struct` info types with nullable fields for optional data. These structs live in `Models/` (one per file).

Before adding a helper, inspect current siblings matching `*DiscoveryHelper.cs` and
`*AttributeHelper.cs`. New features get a dedicated helper rather than extending an
unrelated feature's file.
