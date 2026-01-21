# NexusLabs.Needlr.Hosting

Generic Host support for Needlr - enables auto-discovery and plugin system for worker services and console applications.

## Quick Start

### Option 1: Needlr Controls the Host

```csharp
using NexusLabs.Needlr.Hosting;
using NexusLabs.Needlr.Injection;

var host = new Syringe()
    .ForHost()
    .UsingOptions(() => CreateHostOptions.Default
        .UsingArgs(args)
        .UsingApplicationName("MyWorkerService"))
    .BuildHost();

await host.RunAsync();
```

### Option 2: User Controls the Builder

```csharp
using Microsoft.Extensions.Hosting;
using NexusLabs.Needlr.Hosting;
using NexusLabs.Needlr.Injection;

var builder = Host.CreateApplicationBuilder(args);

// Your own configuration
builder.Services.AddMyServices();

// Add Needlr discovery
builder.UseNeedlrDiscovery();

var host = builder.Build();

// Optionally run IHostPlugin plugins
host.RunHostPlugins();

await host.RunAsync();
```

## Plugin Interfaces

### IHostApplicationBuilderPlugin

Runs during builder configuration (before `Build()`):

```csharp
public sealed class MyBuilderPlugin : IHostApplicationBuilderPlugin
{
    public void Configure(HostApplicationBuilderPluginOptions options)
    {
        options.Builder.Services.AddSingleton<IMyService, MyService>();
    }
}
```

### IHostPlugin

Runs after `Build()` but before `Run()`:

```csharp
public sealed class MyHostPlugin : IHostPlugin
{
    public void Configure(HostPluginOptions options)
    {
        var service = options.Host.Services.GetRequiredService<IMyService>();
        service.Initialize();
    }
}
```

## Key Features

- **`ForHost()`** - Transitions Syringe to host mode, Needlr controls lifecycle
- **`UseNeedlrDiscovery()`** - Integrates discovery into user-controlled builders
- **`IHostedService` auto-discovery** - Background services registered automatically
- **Full parity** between reflection and source-gen modes

## Links

- [Main Needlr Documentation](https://github.com/nexus-labs/needlr)
- [Example Projects](https://github.com/nexus-labs/needlr/tree/main/src/Examples/Hosting)
