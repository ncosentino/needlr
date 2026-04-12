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
// 2. UsingSourceGen() - Uses compile-time generated TypeRegistry
// 3. ForHost() - Transitions Syringe to host mode
// 4. IHostApplicationBuilderPlugin - Configure the host builder before build
// 5. IHostPlugin - Configure the host after build, before run
// 6. IHostedService auto-discovery - Background services registered automatically
// =============================================================================

await new NeedlrBootstrapper()
    .RunAsync(async (context, ct) =>
    {
        var host = new Syringe()
            .UsingSourceGen() // Use source generation instead of reflection
            .ForHost()
            .UsingOptions(() => CreateHostOptions.Default
                .UsingCurrentProcessArgs()
                .UsingApplicationName("WorkerServiceSourceGen")
                .UsingLogger(context.Logger))
            .BuildHost();

        await host.RunAsync(ct);
    });
