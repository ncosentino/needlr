---
description: Automatically discover and register IHostedService and BackgroundService implementations in .NET using Needlr -- no attributes required for hosted service auto-discovery.
---

# Hosted Service Auto-Discovery

Needlr automatically discovers and registers classes that inherit from `BackgroundService` or implement `IHostedService`. No additional attributes are required.

## How It Works

When you use `[GenerateTypeRegistry]`, Needlr scans for:

1. Classes inheriting from `Microsoft.Extensions.Hosting.BackgroundService`
2. Classes directly implementing `Microsoft.Extensions.Hosting.IHostedService`

These are automatically registered with the dual-registration pattern:

```csharp
// Generated code:
services.AddSingleton<MyWorkerService>();
services.AddSingleton<IHostedService>(sp => sp.GetRequiredService<MyWorkerService>());
```

This pattern allows:

- Resolving by concrete type (`MyWorkerService`)
- Resolution via `IHostedService` for the host to start/stop
- Decorators to be applied via `IHostedService`

## Automatic Startup

When using `.ForWebApplication()` or `.ForHost()` to build a `WebApplication` or `IHost`, your discovered hosted services will **automatically start** when you call `StartAsync()` or `RunAsync()`:

```csharp
var app = new Syringe()
    .UsingSourceGen()
    .ForWebApplication()
    .BuildWebApplication();

// Your hosted services start here
await app.RunAsync();
```

The .NET host runtime handles starting all `IHostedService` registrations, including your auto-discovered background services. They will also be stopped when the application shuts down.

## Basic Example

```csharp
// Just inherit from BackgroundService - no attributes needed
public sealed class OrderProcessingWorker : BackgroundService
{
    private readonly IOrderQueue _orderQueue;
    
    public OrderProcessingWorker(IOrderQueue orderQueue)
    {
        _orderQueue = orderQueue;
    }
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var order = await _orderQueue.DequeueAsync(stoppingToken);
            // Process order...
        }
    }
}
```

## Excluding a Hosted Service

Use `[DoNotAutoRegister]` to prevent auto-discovery:

```csharp
[DoNotAutoRegister]
public sealed class ManuallyRegisteredWorker : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken) 
        => Task.CompletedTask;
}
```

## Decorating Hosted Services

You can apply decorators to all hosted services using `[DecoratorFor<IHostedService>]`:

```csharp
[DecoratorFor<IHostedService>(Order = 0)]
public sealed class TimingHostedServiceDecorator : IHostedService
{
    private readonly IHostedService _inner;
    private readonly ILogger<TimingHostedServiceDecorator> _logger;
    
    public TimingHostedServiceDecorator(
        IHostedService inner, 
        ILogger<TimingHostedServiceDecorator> logger)
    {
        _inner = inner;
        _logger = logger;
    }
    
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
        await _inner.StartAsync(cancellationToken);
        _logger.LogInformation("Started {Type} in {Elapsed}ms", 
            _inner.GetType().Name, sw.ElapsedMilliseconds);
    }
    
    public Task StopAsync(CancellationToken cancellationToken) 
        => _inner.StopAsync(cancellationToken);
}
```

### Multi-Level Decoration

Multiple decorators can be stacked using the `Order` property:

```csharp
[DecoratorFor<IHostedService>(Order = 0)]  // Innermost (closest to service)
public sealed class TrackerDecorator : IHostedService { ... }

[DecoratorFor<IHostedService>(Order = 1)]
public sealed class LoggingDecorator : IHostedService { ... }

[DecoratorFor<IHostedService>(Order = 2)]  // Outermost
public sealed class MetricsDecorator : IHostedService { ... }
```

Resolution order: `MetricsDecorator → LoggingDecorator → TrackerDecorator → ActualService`

## Resolution Behavior

```csharp
var provider = new Syringe()
    .UsingSourceGen()
    .BuildServiceProvider();

// Concrete resolution - NOT decorated
var worker = provider.GetRequiredService<OrderProcessingWorker>();

// Interface resolution - decorated
var hostedServices = provider.GetServices<IHostedService>();
// Each IHostedService is wrapped by all applicable decorators
```

## Discovery Rules

A type is discovered as a hosted service if:

- ✅ Inherits from `BackgroundService` OR implements `IHostedService`
- ✅ Is a concrete class (not abstract)
- ✅ Is accessible (public, or internal in the current assembly)
- ❌ Does NOT have `[DoNotAutoRegister]`
- ❌ Does NOT have `[DecoratorFor<IHostedService>]` (decorators are not services)

## Notes

- Hosted services are always registered as **Singleton** lifetime
- Decorators with `[DecoratorFor<IHostedService>]` are excluded from hosted service discovery to prevent circular dependencies
- Works with both source-gen and reflection paths

!!! info "Read More on Dev Leader"
    - [Hosted Services with Needlr: Background Workers and Lifecycle Management](https://www.devleader.ca/2026/02/21/hosted-services-with-needlr-background-workers-and-lifecycle-management)
