using NexusLabs.Needlr.Hosting;
using NexusLabs.Needlr.Injection;
using NexusLabs.Needlr.Injection.Reflection;

using WorkerServiceExample;

// =============================================================================
// WorkerServiceExample: Demonstrates Needlr's Generic Host Support
// =============================================================================
// This example shows how to use Needlr with the Generic Host for worker services
// and console applications. Needlr provides the same auto-discovery and plugin
// system that it provides for ASP.NET Core, but for non-web scenarios.
//
// Key features demonstrated:
// 1. ForHost() - Transitions Syringe to host mode
// 2. IHostApplicationBuilderPlugin - Configure the host builder before build
// 3. IHostPlugin - Configure the host after build, before run
// 4. IHostedService auto-discovery - Background services registered automatically
// 5. CreateHostOptions - Fluent configuration for the host
// =============================================================================

var host = new Syringe()
    .UsingReflection() // Use reflection for this example
    .ForHost()
    .UsingOptions(() => CreateHostOptions.Default
        .UsingArgs(args)
        .UsingApplicationName("WorkerServiceExample")
        .UsingStartupConsoleLogger())
    .BuildHost();

await host.RunAsync();
