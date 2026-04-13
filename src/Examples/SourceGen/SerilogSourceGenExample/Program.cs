using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using NexusLabs.Needlr.Injection;
using NexusLabs.Needlr.Injection.SourceGen;

// ─────────────────────────────────────────────────────────────────────────────
// Serilog + Source Generation Example
//
// Demonstrates zero-ceremony Serilog wiring via SerilogPlugin:
//   1. Reference NexusLabs.Needlr.Serilog
//   2. Add a "Serilog" section to appsettings.json
//   3. Call UsingSourceGen() — SerilogPlugin is auto-discovered via
//      [ModuleInitializer] bootstrap, no UsingAdditionalAssemblies needed.
//
// The source generator emits a TypeRegistry for this assembly (triggered by
// the AppMarker class below), which in turn emits a [ModuleInitializer] that
// aggregates plugins from all referenced assemblies — including
// NexusLabs.Needlr.Serilog's SerilogPlugin.
// ─────────────────────────────────────────────────────────────────────────────

var config = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", optional: false)
    .Build();

var serviceProvider = new Syringe()
    .UsingSourceGen()
    .BuildServiceProvider(config);

var logger = serviceProvider.GetRequiredService<ILogger<SerilogSourceGenExample.AppMarker>>();

Console.WriteLine("SerilogSourceGenExample");
Console.WriteLine("=======================");
Console.WriteLine("SerilogPlugin auto-discovered via [ModuleInitializer] bootstrap.");
Console.WriteLine("ILogger<T> configured from appsettings.json — no manual setup:");
Console.WriteLine();

logger.LogDebug("Debug-level message with {Source}", "source-gen discovery");
logger.LogInformation("Application started with {ConfigSource}", "appsettings.json");
logger.LogWarning("Example warning: {Metric} exceeded threshold of {Threshold}", "latency_ms", 500);

namespace SerilogSourceGenExample
{
    /// <summary>
    /// Marker type that triggers the source generator to emit a TypeRegistry for this
    /// assembly. Without at least one type in the assembly's namespace, the generator
    /// skips emitting the [ModuleInitializer] bootstrap.
    /// </summary>
    internal sealed class AppMarker;
}
