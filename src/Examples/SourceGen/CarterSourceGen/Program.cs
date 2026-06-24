using Carter;

using Microsoft.Extensions.DependencyInjection;

using NexusLabs.Needlr.AspNet;
using NexusLabs.Needlr.Injection;
using NexusLabs.Needlr.Injection.SourceGen;

// ─────────────────────────────────────────────────────────────────────────────
// CarterSourceGen — demonstrates NexusLabs.Needlr.Carter on the source-generated
// ASP.NET Core WebApplication path with a PUBLIC Carter module.
//
// This is the configuration that previously triggered a duplicate registration:
// a public ICarterModule was discovered by BOTH Carter's own AddCarter() scan and
// Needlr's source-gen type registry, so MapCarter() mapped each route twice and the
// endpoint returned an AmbiguousMatchException.
//
// The NexusLabs.Needlr.Carter plugin now calls AddCarter(c => c.WithEmptyModules()),
// so Carter no longer scans for modules and Needlr's type registry is the single
// registrar. The public module below is registered exactly once and GET /api/ping
// returns 200.
//
// The example self-checks: if PingModule resolves anything other than exactly once,
// it sets a non-zero exit code (the bug signature), so the example doubles as a
// runnable regression guard.
// ─────────────────────────────────────────────────────────────────────────────

var webApplication = new Syringe()
    .UsingSourceGen()
    .ForWebApplication()
    .BuildWebApplication();

var webAppTask = webApplication.RunAsync();

var pingModuleRegistrations = webApplication.Services
    .GetServices<ICarterModule>()
    .Count(module => module is CarterSourceGen.PingModule);

Console.WriteLine("CarterSourceGen Example");
Console.WriteLine("=======================");
Console.WriteLine("Public Carter module discovered through Needlr source generation.");
Console.WriteLine();
Console.WriteLine($"  ICarterModule registrations for PingModule: {pingModuleRegistrations}");

if (pingModuleRegistrations == 1)
{
    Console.WriteLine("  ✓  Registered exactly once — GET /api/ping serves a single endpoint.");
}
else
{
    Console.WriteLine(
        $"  ⚠  Expected exactly 1 registration but found {pingModuleRegistrations}. This is the");
    Console.WriteLine(
        "     duplicate-registration bug signature and would cause an AmbiguousMatchException.");
    Environment.ExitCode = 1;
}

Console.WriteLine();
Console.WriteLine("  GET /api/ping  →  {\"message\":\"pong\"}");
Console.WriteLine();

await webAppTask;
