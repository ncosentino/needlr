# NDLRCOR011: Keyed Service Usage

## Cause

A constructor parameter uses `[FromKeyedServices("key")]` to resolve a keyed service dependency.

## Rule Description

When using source generation with `[assembly: GenerateTypeRegistry]`, Needlr detects parameters annotated with `[FromKeyedServices]`. This analyzer provides informational tracking of keyed service usage patterns in your codebase.

```csharp
// ℹ️ NDLRCOR011: Parameter 'processor' uses keyed service resolution with key "primary"
public class PaymentHandler(
    [FromKeyedServices("primary")] IPaymentProcessor processor)
{
    // Resolved via GetRequiredKeyedService<IPaymentProcessor>("primary")
}
```

This is an **informational diagnostic** (not a warning or error) because:
- Keyed services are typically registered at runtime via `IServiceCollectionPlugin`
- The analyzer cannot validate that the key is actually registered
- Both source-gen and reflection paths support keyed services

## How Keyed Services Work

The source generator correctly handles `[FromKeyedServices]` and generates appropriate resolution code:

```csharp
// Generated factory lambda
services.AddSingleton<PaymentHandler>(sp => new PaymentHandler(
    sp.GetRequiredKeyedService<IPaymentProcessor>("primary")));
```

### Registering Keyed Services

Register keyed services via a plugin:

```csharp
public class PaymentServicesPlugin : IServiceCollectionPlugin
{
    public void Configure(IServiceCollection services)
    {
        services.AddKeyedSingleton<IPaymentProcessor, StripeProcessor>("primary");
        services.AddKeyedSingleton<IPaymentProcessor, PayPalProcessor>("backup");
    }
}
```

### Mixed Keyed and Non-Keyed Parameters

The generator handles constructors with both keyed and non-keyed parameters:

```csharp
public class OrderProcessor(
    [FromKeyedServices("primary")] IPaymentProcessor payment,
    ILogger<OrderProcessor> logger)  // Non-keyed - uses GetRequiredService
{
}
```

## How to Use This Diagnostic

### Track Keyed Service Usage

Use the diagnostic to maintain awareness of keyed service patterns:
- Find all usages: Search for NDLRCOR011 in your IDE
- Review dependencies: Ensure matching registrations exist in plugins

### Suppress If Not Needed

If you don't need keyed service tracking:

```ini
# .editorconfig
[*.cs]
dotnet_diagnostic.NDLRCOR011.severity = none
```

### Promote to Warning

For stricter validation (manual review of keyed service registrations):

```ini
# .editorconfig
[*.cs]
dotnet_diagnostic.NDLRCOR011.severity = warning
```

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

## Limitations

This analyzer **cannot** validate:
- Whether the keyed service is actually registered
- Whether the key value matches a registration
- Lifetime compatibility of keyed dependencies

Keyed services are registered at runtime via plugins, so compile-time validation of keys and lifetimes is not possible. The existing `LifetimeMismatchAnalyzer` (NDLRCOR005) validates lifetimes for statically-discovered services.

## See Also

- [Keyed Services](../keyed-services.md) - How keyed services work in Needlr
- [AnalyzerStatus.md](../breadcrumbs.md#analyzer-status) - View all active analyzers in diagnostics output
