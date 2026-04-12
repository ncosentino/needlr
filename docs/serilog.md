---
description: Serilog bootstrap integration for Needlr applications — two-stage init, structured logging before DI, and automatic log flushing on shutdown.
---

# Serilog Bootstrap

The `NexusLabs.Needlr.Serilog` package provides `NeedlrSerilogBootstrapper`, a thin wrapper around
[`NeedlrBootstrapper`](advanced-usage.md#application-bootstrap-lifecycle) that adds Serilog-specific lifecycle
management: two-stage initialization, a structured pre-DI logger, and automatic `Log.CloseAndFlushAsync` on shutdown.

## Quick Start

```csharp
using NexusLabs.Needlr.Serilog;

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

`ctx.Logger` is a `Microsoft.Extensions.Logging.ILogger` backed by a Serilog pipeline. By default a console sink
is configured. The bootstrapper sets the global `Log.Logger`, so Serilog's static API is also available inside the
callback.

## Custom Serilog Configuration

Use `.Configure(Action<LoggerConfiguration>)` to replace the default console sink with your own pipeline:

```csharp
await new NeedlrSerilogBootstrapper()
    .Configure(cfg => cfg
        .MinimumLevel.Debug()
        .WriteTo.Console()
        .WriteTo.File("logs/startup.log"))
    .RunAsync(async (ctx, ct) =>
    {
        // ctx.Logger routes through the configured pipeline
        ctx.Logger.LogInformation("Application starting with custom Serilog config...");
        // ...
    });
```

## ASP.NET Core Example

```csharp
using NexusLabs.Needlr.AspNet;
using NexusLabs.Needlr.Serilog;

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

## How It Composes with NeedlrBootstrapper

`NeedlrSerilogBootstrapper` is not a parallel implementation — it delegates all lifecycle management to
`NeedlrBootstrapper` internally:

1. Applies your `LoggerConfiguration` (or defaults to `WriteTo.Console()`).
2. Sets `Log.Logger` globally.
3. Creates a `SerilogLoggerFactory` and passes it to `NeedlrBootstrapper.UsingLoggerFactory(...)`.
4. Registers `Log.CloseAndFlushAsync` via `NeedlrBootstrapper.WithCleanup(...)`.
5. Calls `NeedlrBootstrapper.RunAsync(...)` — which handles exception catching, cleanup, and factory lifetime.

This means you get all `NeedlrBootstrapper` guarantees automatically:

- Exceptions are caught, logged at `Critical`, and do **not** rethrow.
- `Log.CloseAndFlushAsync` is called in `finally`, even if the callback throws.
- `Log.Logger` is available via the static Serilog API throughout the application lifetime.

## Installation

```xml
<PackageReference Include="NexusLabs.Needlr.Serilog" />
```

## API Reference

### `NeedlrSerilogBootstrapper`

| Member | Description |
|--------|-------------|
| `RunAsync(Func<NeedlrBootstrapContext, CancellationToken, Task>, CancellationToken)` | Runs the application with full Serilog bootstrap lifecycle. |

### `NeedlrSerilogBootstrapperExtensions`

| Extension | Description |
|-----------|-------------|
| `.Configure(Action<LoggerConfiguration>)` | Replaces the default `WriteTo.Console()` with a custom Serilog configuration. Returns a new instance. |
