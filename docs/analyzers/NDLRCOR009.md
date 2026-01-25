# NDLRCOR009: Lazy<T> references undiscovered type

## Cause

A constructor parameter of type `Lazy<T>` references a type `T` that was not discovered by source generation.

## Rule Description

When using source generation with `[assembly: GenerateTypeRegistry]`, Needlr scans your codebase to discover injectable types. This analyzer detects when a `Lazy<T>` dependency references a type that wasn't found during that scan.

```csharp
// ⚠️ NDLRCOR009: IUnknownService not discovered
public class OrderService(Lazy<IUnknownService> unknown)
{
    // ...
}
```

This is an **informational diagnostic** (not a warning or error) because:
- The type may be registered via reflection at runtime
- The type may come from a third-party library
- The type may be registered manually in an `IServiceCollectionPlugin`

## How to Fix

### Option 1: Ensure the Type is Discoverable

Make sure the implementation is in a namespace matching your `IncludeNamespacePrefixes`:

```csharp
[assembly: GenerateTypeRegistry(IncludeNamespacePrefixes = new[] { "MyApp" })]

namespace MyApp.Services
{
    public interface IUnknownService { }
    
    // ✅ This will be discovered
    public class UnknownService : IUnknownService { }
}
```

### Option 2: Register Manually via Plugin

If the type is intentionally registered elsewhere:

```csharp
public class MyPlugin : IServiceCollectionPlugin
{
    public void Configure(IServiceCollection services)
    {
        // Registered manually - Lazy<IExternalService> will work
        services.AddSingleton<IExternalService, ExternalService>();
    }
}
```

### Option 3: Suppress the Diagnostic

If you know the type will be available at runtime:

```csharp
#pragma warning disable NDLRCOR009
public class OrderService(Lazy<IExternalService> external)
#pragma warning restore NDLRCOR009
{
    // ...
}
```

Or suppress project-wide in `.editorconfig`:

```ini
# .editorconfig
[*.cs]
dotnet_diagnostic.NDLRCOR009.severity = none
```

## When This Analyzer Activates

This analyzer only runs when `[assembly: GenerateTypeRegistry]` is present. In reflection-only projects, there's no type registry to validate against.

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
dotnet_diagnostic.NDLRCOR009.severity = warning
```

## See Also

- [Lazy Injection](../core-concepts.md#lazy-injection) - How `Lazy<T>` works in Needlr
- [AnalyzerStatus.md](../breadcrumbs.md#analyzer-status) - View all active analyzers in diagnostics output
