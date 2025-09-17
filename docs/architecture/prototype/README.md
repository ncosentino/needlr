# Prototype Usage Examples

This directory contains prototype implementations demonstrating how Needlr could integrate with Microsoft.Extensions.Hosting patterns.

## Key Files

- `NeedlrHost.cs` - Entry point class providing `CreateDefaultBuilder()` like Microsoft's Host class
- `NeedlrHostBuilder.cs` - Implementation of `IHostBuilder` that integrates Needlr capabilities
- `UsageExamples.cs` - Concrete examples showing different integration patterns

## Usage Patterns

### 1. Drop-in Replacement Pattern
```csharp
// Instead of:
var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((context, services) =>
    {
        services.AddSingleton<IMyService, MyService>();
        // ... many more manual registrations
    })
    .Build();

// Use:
var host = NeedlrHost.CreateDefaultBuilder(args)
    .ConfigureNeedlr(syringe => syringe
        .UsingScrutorTypeRegistrar()) // Automatic registration!
    .ConfigureServices((context, services) =>
    {
        // Only register things that need special configuration
        services.AddHostedService<MyBackgroundService>();
    })
    .Build();
```

### 2. Integration with Existing Applications
```csharp
var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((context, services) =>
    {
        // Existing manual registrations
        services.AddSingleton<ILegacyService, LegacyService>();
        
        // Add Needlr for new services
        services.AddNeedlr(syringe => syringe
            .UsingScrutorTypeRegistrar()
            .UsingAssemblyProvider(builder => builder
                .MatchingAssemblies(x => x.Contains("NewFeatures"))
                .Build()));
    })
    .Build();
```

### 3. Pure Needlr with Hosting Features
```csharp
var host = new Syringe()
    .UsingScrutorTypeRegistrar()
    .AsHost() // Convert to hosting-aware Syringe
    .ConfigureServices((context, services) =>
    {
        services.AddHostedService<MyBackgroundService>();
    })
    .BuildHost();
```

## Benefits Demonstrated

1. **Familiar API**: Uses same patterns as Microsoft.Extensions.Hosting
2. **Incremental Adoption**: Can be added to existing applications
3. **Automatic Registration**: Maintains Needlr's key benefit
4. **Full Hosting Support**: Supports IHostedService, configuration, logging, etc.
5. **Backward Compatibility**: Existing Needlr code continues to work