using Microsoft.Extensions.Configuration;

using NexusLabs.Needlr.Hosting;
using NexusLabs.Needlr.Injection;
using NexusLabs.Needlr.Injection.SourceGen;

using WorkerServiceSourceGen;

// =============================================================================
// WorkerServiceSourceGen: Demonstrates Needlr's Generic Host Support with Source Gen
// =============================================================================
// This example shows how to use Needlr with the Generic Host using source generation
// instead of reflection. This enables:
// 1. AOT compatibility - No reflection at runtime
// 2. Better startup performance - Types discovered at compile time
// 3. Trimming support - Unused code can be trimmed
//
// Key features demonstrated:
// 1. NeedlrBootstrapper - Wraps the entry point with a pre-DI bootstrap logger,
//    top-level exception handling, and cleanup
// 2. BootstrapConfiguration - Pre-DI IConfiguration for config-driven bootstrap
// 3. UsingSourceGen() - Uses compile-time generated TypeRegistry
// 4. ForHost() - Transitions Syringe to host mode
// 5. IHostApplicationBuilderPlugin - Configure the host builder before build
// 6. IHostPlugin - Configure the host after build, before run
// 7. IHostedService auto-discovery - Background services registered automatically
// =============================================================================

await new NeedlrBootstrapper()
    .ConfigureBootstrapConfiguration(builder =>
    {
        // This configuration is ONLY for the bootstrap phase (before DI
        // exists). It is NOT forwarded to the application's IConfiguration.
        // Once the DI container builds, the host's own IConfiguration takes
        // over — driven by appsettings.json, environment variables, etc.
        builder.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Bootstrap:AppName"] = "WorkerServiceSourceGen",
        });
    })
    .RunAsync(async (context, ct) =>
    {
        // Bootstrap configuration is available before DI exists
        var appName = context.BootstrapConfiguration["Bootstrap:AppName"]
            ?? "WorkerServiceSourceGen";
        context.Logger.LogInformation(
            "Bootstrap phase: starting {AppName}",
            appName);

        var host = new Syringe()
            .UsingSourceGen() // Use source generation instead of reflection
            .ForHost()
            .UsingOptions(() => CreateHostOptions.Default
                .UsingCurrentProcessArgs()
                .UsingApplicationName(appName)
                .UsingLogger(context.Logger))
            .BuildHost();

        await host.RunAsync(ct);
    });
