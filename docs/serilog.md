---
description: Serilog integration for Needlr — zero-ceremony plugin for ILogger<T> wiring, config-driven setup from appsettings.json, override patterns, and bootstrapper lifecycle management.
---

# Serilog Integration

The `NexusLabs.Needlr.Serilog` package provides two ways to use Serilog with Needlr:

1. **`SerilogPlugin`** — an auto-discovered `IServiceCollectionPlugin` that wires `ILogger<T>` from `appsettings.json` with zero ceremony. Best for most applications.
2. **`NeedlrSerilogBootstrapper`** — a lifecycle wrapper for applications that need pre-DI structured logging, the static `Log.Logger`, and automatic flush-on-shutdown. Best for hosted services and ASP.NET apps with strict startup logging requirements.

## Installation

```xml
<PackageReference Include="NexusLabs.Needlr.Serilog" />
```

The package includes both the plugin and the bootstrapper. Use whichever fits your scenario — they are independent and do not conflict.

---

## SerilogPlugin (Recommended for Most Apps)

### Quick Start

1. Reference `NexusLabs.Needlr.Serilog`
2. Add a `"Serilog"` section to `appsettings.json`
3. Call `UsingSourceGen()` or `UsingReflection()` — the plugin is auto-discovered

```json
{
  "Serilog": {
    "Using": ["Serilog.Sinks.Console"],
    "MinimumLevel": "Information",
    "WriteTo": [
      { "Name": "Console" }
    ]
  }
}
```

```csharp
var config = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json")
    .Build();

var sp = new Syringe()
    .UsingSourceGen()
    .BuildServiceProvider(config);

var logger = sp.GetRequiredService<ILogger<MyService>>();
logger.LogInformation("Running with Serilog — no manual setup");
```

That's it. No `AddSerilog()`, no `Log.Logger`, no ceremony. The plugin reads the `"Serilog"` section from `IConfiguration`, configures the logger, and registers it with `dispose: true` so sinks flush when the DI container is disposed.

### How It Works

`SerilogPlugin` implements `IServiceCollectionPlugin` and is auto-discovered by Needlr's plugin system:

- **Source-gen mode**: The plugin's assembly has `[GenerateTypeRegistry]`, so the source generator emits a `[ModuleInitializer]` that registers `SerilogPlugin` into the `NeedlrSourceGenBootstrap`. When you call `UsingSourceGen()`, the plugin is discovered automatically.
- **Reflection mode**: The reflection scanner finds `SerilogPlugin` when its assembly is in the scan path. Use `UsingAdditionalAssemblies` if needed:

```csharp
var sp = new Syringe()
    .UsingReflection()
    .UsingAdditionalAssemblies([typeof(SerilogPlugin).Assembly])
    .BuildServiceProvider(config);
```

### Overriding the Plugin's Configuration

The plugin gives you a working default — Serilog configured from `appsettings.json`. If you need to replace or extend that configuration (e.g., add a custom sink, change the minimum level programmatically), use `UsingPostPluginRegistrationCallback`:

```csharp
var sp = new Syringe()
    .UsingSourceGen()
    .UsingPostPluginRegistrationCallback(services =>
    {
        services.AddLogging(builder =>
        {
            builder.ClearProviders();
            builder.AddSerilog(
                new LoggerConfiguration()
                    .MinimumLevel.Debug()
                    .WriteTo.Console()
                    .WriteTo.File("logs/app.log")
                    .CreateLogger(),
                dispose: true);
        });
    })
    .BuildServiceProvider(config);
```

The override runs after the plugin, so it replaces the plugin's config-driven logger with your explicit one. `ILogger<T>` resolution continues to work because the plugin already registered the `ILoggerFactory` open generic — you're just swapping the underlying Serilog pipeline.

### What the Plugin Does NOT Do

- **Does not set `Log.Logger`** — use `ILogger<T>` injection instead. If you need the static logger, use `NeedlrSerilogBootstrapper`.
- **Does not provide pre-DI logging** — the plugin runs during DI registration, not before. If you need to log during startup before the container is built, use `NeedlrSerilogBootstrapper`.

---

## NeedlrSerilogBootstrapper (Lifecycle Management)

For applications that need structured logging before the DI container exists (e.g., logging configuration errors, startup diagnostics), the bootstrapper provides a two-stage lifecycle.

### Quick Start

```csharp
await new NeedlrSerilogBootstrapper()
    .RunAsync(async (ctx, ct) =>
    {
        var host = new Syringe()
            .UsingSourceGen()
            .ForHost()
            .UsingOptions(() => CreateHostOptions.Default
                .UsingCurrentProcessArgs()
                .UsingLogger(ctx.Logger))
            .BuildHost();

        await host.RunAsync(ct);
    });
```

`ctx.Logger` is a `Microsoft.Extensions.Logging.ILogger` backed by the Serilog pipeline. By default a console sink is configured. The bootstrapper sets the global `Log.Logger`, so Serilog's static API is also available.

### Custom Configuration

```csharp
await new NeedlrSerilogBootstrapper()
    .Configure(cfg => cfg
        .MinimumLevel.Debug()
        .WriteTo.Console()
        .WriteTo.File("logs/startup.log"))
    .RunAsync(async (ctx, ct) =>
    {
        ctx.Logger.LogInformation("Application starting...");
        // ...
    });
```

### ASP.NET Core Example

```csharp
await new NeedlrSerilogBootstrapper()
    .Configure(cfg => cfg
        .MinimumLevel.Information()
        .WriteTo.Console())
    .RunAsync(async (ctx, ct) =>
    {
        var webApp = new Syringe()
            .UsingSourceGen()
            .ForWebApplication()
            .UsingOptions(() => CreateWebApplicationOptions.Default
                .UsingCurrentProcessCliArgs()
                .UsingLogger(ctx.Logger))
            .BuildWebApplication();

        await webApp.RunAsync(ct);
    });
```

### Lifecycle Guarantees

`NeedlrSerilogBootstrapper` composes `NeedlrBootstrapper` internally:

1. Applies your `LoggerConfiguration` (or defaults to `WriteTo.Console()`).
2. Sets `Log.Logger` globally.
3. Creates a `SerilogLoggerFactory` and passes it to `NeedlrBootstrapper.UsingLoggerFactory(...)`.
4. Registers `Log.CloseAndFlushAsync` via `NeedlrBootstrapper.WithCleanup(...)`.
5. Calls `NeedlrBootstrapper.RunAsync(...)` for exception catching, cleanup, and factory lifetime.

This guarantees:

- Exceptions are caught, logged at `Critical`, and do **not** rethrow.
- `Log.CloseAndFlushAsync` is called in `finally`, even if the callback throws.
- `Log.Logger` is available via the static Serilog API throughout the application lifetime.

---

## Choosing Between Plugin and Bootstrapper

| Scenario | Use |
|----------|-----|
| Standard app, logging configured from appsettings.json | `SerilogPlugin` |
| Need `ILogger<T>` injection with zero ceremony | `SerilogPlugin` |
| Need to log before DI container is built | `NeedlrSerilogBootstrapper` |
| Need the static `Log.Logger` / `Log.Information(...)` API | `NeedlrSerilogBootstrapper` |
| Need guaranteed flush-on-shutdown (buffered file sinks) | Either — plugin uses `dispose: true`, bootstrapper uses `CloseAndFlushAsync` |
| ASP.NET app with startup logging requirements | `NeedlrSerilogBootstrapper` |

Both can coexist in the same application, but typically you choose one.
