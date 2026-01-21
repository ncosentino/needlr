using Microsoft.Extensions.Hosting;

using NexusLabs.Needlr.Hosting;
using NexusLabs.Needlr.Injection;
using NexusLabs.Needlr.Injection.SourceGen;

using HostBuilderIntegrationSourceGen;

// =============================================================================
// HostBuilderIntegrationSourceGen: UseNeedlrDiscovery() with Source Generation
// =============================================================================
// This example shows how to integrate Needlr's auto-discovery into your own
// HostApplicationBuilder workflow using source generation. Use this pattern when:
// - You have existing host configuration code you want to preserve
// - You need fine-grained control over the builder lifecycle
// - You're integrating Needlr into a larger framework
// - You want AOT compatibility and trimming support
//
// Key features demonstrated:
// 1. UsingSourceGen() - Uses compile-time generated TypeRegistry
// 2. UseNeedlrDiscovery() - Adds Needlr discovery to user-controlled builder
// 3. IHostApplicationBuilderPlugin - Still runs during discovery
// 4. RunHostPlugins() - Opt-in post-build IHostPlugin execution
// 5. Mixed configuration - Your code + Needlr discovery coexist
// =============================================================================

// Create the builder yourself - YOU control its lifecycle
var builder = Host.CreateApplicationBuilder(args);

// Add your own configuration first
builder.Configuration.AddJsonFile("appsettings.json", optional: true);
builder.Services.AddSingleton<ICustomService, CustomService>();

Console.WriteLine("Adding Needlr discovery (SourceGen) to user-controlled builder...");

// Integrate Needlr discovery with source generation - this runs:
// 1. IHostApplicationBuilderPlugin plugins
// 2. IServiceCollectionPlugin plugins  
// 3. Auto-discovers and registers all types (at compile time)
var syringe = new Syringe().UsingSourceGen();
builder.UseNeedlrDiscovery(syringe);

// Add more of your own configuration after Needlr
builder.Services.AddHostedService<IntegrationExampleWorker>();

Console.WriteLine("Building host...");
var host = builder.Build();

// Optionally run IHostPlugin plugins (Needlr couldn't run them automatically
// because you control when Build() is called)
Console.WriteLine("Running IHostPlugin plugins...");
host.RunHostPlugins();

Console.WriteLine("Starting host...");
await host.RunAsync();
