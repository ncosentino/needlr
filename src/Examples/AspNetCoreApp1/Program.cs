using NexusLabs.Needlr.AspNet;
using NexusLabs.Needlr.Injection;
using NexusLabs.Needlr.Injection.Scrutor;

var webApplication = new Syringe()
    .UsingScrutorTypeRegistrar()
    .UsingAssemblyProvider(builder => builder
        .MatchingAssemblies(x =>
            x.Contains("NexusLabs", StringComparison.OrdinalIgnoreCase) ||
            x.Contains("AspNetCoreApp1", StringComparison.OrdinalIgnoreCase))
        .UseLibTestEntrySorting()
        .Build())
    .UsingAdditionalAssemblies(additionalAssemblies: [])
    .ForWebApplication()
    .UsingOptions(() => CreateWebApplicationOptions
        .Default
        .UsingStartupConsoleLogger())
    .BuildWebApplication();

await webApplication.RunAsync();
