---
applyTo: "**/Models/**/*.cs"
---

# Discovery Model Rules

## One type per file

Never put multiple types in one `.cs` file. Each `struct`, `enum`, or `class` gets its own file named after the type.

## Type shape

- Use `internal readonly struct` for immutable discovery-result models.
- Use `internal enum` for kind/flag enumerations. Apply `[Flags]` when values are composable.
- Constructor parameters should match the property list exactly.

## Folder organization

Organize into logical subfolders by feature area when 2+ related types cluster:

```
Models/
├── DiscoveryResult.cs          (top-level aggregator)
├── DiscoveredType.cs           (standalone types at root)
├── Options/                    (feature subfolder)
│   ├── DiscoveredOptions.cs
│   ├── OptionsPropertyInfo.cs
│   └── ComplexTypeKind.cs
├── HttpClients/
│   ├── DiscoveredHttpClient.cs
│   └── HttpClientCapabilities.cs
└── Providers/
    ├── DiscoveredProvider.cs
    └── ProviderPropertyKind.cs
```

## Namespace

Namespace stays flat at the parent `Models` namespace (e.g., `NexusLabs.Needlr.Generators.Models`) regardless of subfolder. This is a deliberate choice to avoid consumer churn — all types are `internal` with no public API exposure.
