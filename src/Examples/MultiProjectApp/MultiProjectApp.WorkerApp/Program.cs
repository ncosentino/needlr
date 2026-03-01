using NexusLabs.Needlr.Hosting;
using NexusLabs.Needlr.Injection;
using NexusLabs.Needlr.Injection.SourceGen;

// All plugins from Bootstrap and feature projects are discovered via the source-generated
// TypeRegistry. NotificationWorker (a BackgroundService) is auto-discovered and registered
// as a hosted service by Needlr from the WorkerApp assembly's TypeRegistry.
var host = new Syringe()
    .UsingSourceGen()
    .ForHost()
    .UsingOptions(() => CreateHostOptions.Default.UsingArgs(args))
    .BuildHost();

await host.RunAsync();
