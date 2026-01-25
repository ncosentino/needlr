# NDLRCOR011: Keyed Service Unknown Key

## Cause

A constructor parameter uses `[FromKeyedServices("key")]` to resolve a keyed service,
but no `[Keyed("key")]` registration was found in the compilation.

## Rule Description

When using source generation with `[assembly: GenerateTypeRegistry]`, Needlr validates
that `[FromKeyedServices("key")]` parameters reference keys that are statically discoverable.

```csharp
// ℹ️ NDLRCOR011: No registration found for key "unknown"
public class PaymentHandler(
    [FromKeyedServices("unknown")] IPaymentProcessor processor)
{
}
```

This is an **informational diagnostic** (not a warning or error) because:
- Keys may be registered at runtime via `IServiceCollectionPlugin`
- The analyzer only validates statically-discoverable `[Keyed]` registrations

## How to Fix

### Option 1: Add [Keyed] Attribute

Register the service with a matching key:

```csharp
[Keyed("primary")]
public class StripeProcessor : IPaymentProcessor { }

// ✅ Now validated - "primary" key is discovered
public class PaymentHandler(
    [FromKeyedServices("primary")] IPaymentProcessor processor)
{
}
```

### Option 2: Register via Plugin (Suppress Diagnostic)

If registering via plugin, suppress the diagnostic:

```csharp
// Plugin registers keyed services at runtime
public class PaymentPlugin : IServiceCollectionPlugin
{
    public void Configure(ServiceCollectionPluginOptions options)
    {
        options.Services.AddKeyedSingleton<IPaymentProcessor, StripeProcessor>("primary");
    }
}

// Suppress because key is registered via plugin
#pragma warning disable NDLRCOR011
public class PaymentHandler(
    [FromKeyedServices("primary")] IPaymentProcessor processor)
#pragma warning restore NDLRCOR011
{
}
```

### Option 3: Project-Wide Suppression

```ini
# .editorconfig
[*.cs]
dotnet_diagnostic.NDLRCOR011.severity = none
```

## Validation Logic

The analyzer:
1. Collects all `[Keyed("key")]` attributes from classes in the compilation
2. Collects all `[FromKeyedServices("key")]` parameters
3. Reports diagnostic when a key is not found in discovered registrations

Keys registered via plugins cannot be validated at compile time.

## When This Analyzer Activates

This analyzer only runs when:
1. `[assembly: GenerateTypeRegistry]` is present
2. A constructor parameter has `[FromKeyedServices("key")]` attribute
3. The parameter type is not a framework type (System.*, Microsoft.*)

## Severity Levels

| Level | Meaning |
|-------|---------|
| `info` (default) | Informational hint in IDE |
| `warning` | Promote to build warning |
| `error` | Fail the build |
| `none` | Disable completely |

## See Also

- [Keyed Attribute](../attributes/Keyed.md) - Static keyed service registration
- [AnalyzerStatus.md](../breadcrumbs.md#analyzer-status) - View all active analyzers
