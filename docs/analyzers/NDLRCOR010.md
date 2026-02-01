# NDLRCOR010: IEnumerable<T> has no discovered implementations

## Cause

A constructor parameter of type `IEnumerable<T>` references an interface `T` that has no implementations discovered by source generation.

## Rule Description

When using source generation with `[assembly: GenerateTypeRegistry]`, Needlr scans your codebase to discover injectable types. This analyzer detects when an `IEnumerable<T>` dependency references an interface with no discovered implementations.

```csharp
// ⚠️ NDLRCOR010: No implementations of IPlugin discovered
public class PluginHost(IEnumerable<IPlugin> plugins)
{
    // plugins will be empty unless registered via reflection
}
```

This is an **informational diagnostic** (not a warning or error) because:

- Implementations may be registered via reflection at runtime
- Implementations may come from a third-party library
- Implementations may be registered manually in an `IServiceCollectionPlugin`
- An empty collection is valid in some scenarios

## How to Fix

### Option 1: Add Implementations

Create classes that implement the interface:

```csharp
public interface IPlugin { }

// ✅ These will be discovered and registered
public class LoggingPlugin : IPlugin { }
public class CachingPlugin : IPlugin { }
public class MetricsPlugin : IPlugin { }
```

### Option 2: Ensure Namespace Matches

Make sure implementations are in a namespace matching your `IncludeNamespacePrefixes`:

```csharp
[assembly: GenerateTypeRegistry(IncludeNamespacePrefixes = new[] { "MyApp" })]

namespace MyApp.Plugins  // ✅ Matches prefix
{
    public class MyPlugin : IPlugin { }
}

namespace ThirdParty.Plugins  // ❌ Won't be discovered
{
    public class ExternalPlugin : IPlugin { }
}
```

### Option 3: Register Manually via Plugin

For external implementations:

```csharp
public class ExternalPluginRegistration : IServiceCollectionPlugin
{
    public void Configure(IServiceCollection services)
    {
        services.AddSingleton<IPlugin, ThirdPartyPlugin>();
    }
}
```

### Option 4: Suppress the Diagnostic

If empty collections are expected:

```csharp
#pragma warning disable NDLRCOR010
public class PluginHost(IEnumerable<IOptionalPlugin> plugins)
#pragma warning restore NDLRCOR010
{
    // Empty collection is valid here
}
```

Or suppress project-wide in `.editorconfig`:

```ini
# .editorconfig
[*.cs]
dotnet_diagnostic.NDLRCOR010.severity = none
```

## When This Analyzer Activates

This analyzer only runs when:
1. `[assembly: GenerateTypeRegistry]` is present
2. The `IEnumerable<T>` parameter uses an interface type (not a concrete class)
3. The interface is not a framework type (System.*, Microsoft.*)

## Severity Levels

| Level | Meaning |
|-------|---------|
| `info` (default) | Informational hint in IDE |
| `warning` | Promote to build warning |
| `error` | Fail the build |
| `none` | Disable completely |

Configure in `.editorconfig`:

```ini
# Promote to warning for stricter validation
dotnet_diagnostic.NDLRCOR010.severity = warning
```

## See Also

- [Core Concepts](../core-concepts.md) - How `IEnumerable<T>` works in Needlr
- [AnalyzerStatus.md](../breadcrumbs.md#analyzer-status) - View all active analyzers in diagnostics output
