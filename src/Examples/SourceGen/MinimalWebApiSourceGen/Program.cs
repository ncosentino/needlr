using Microsoft.Extensions.Options;

using MinimalWebApiSourceGen;

using NexusLabs.Needlr.AspNet;
using NexusLabs.Needlr.Injection;
using NexusLabs.Needlr.Injection.SourceGen;

// ─────────────────────────────────────────────────────────────────────────────
// MinimalWebApiSourceGen — demonstrates the Needlr source generator on the
// ASP.NET Core WebApplication path, with a focus on the [Options]
// source-gen pattern binding through BuildWebApplication().
//
// Things this example is deliberately proving end-to-end:
//
//   1. Source-generated type discovery replaces reflection for injectable
//      services (WeatherPlugin, WeatherProvider).
//
//   2. The [Options] source generator also runs through the web path: the
//      generated TypeRegistry.RegisterOptions method is invoked from inside
//      WebApplicationSyringe.BuildWebApplication(), so
//      IOptions<WeatherOptions>.Value is populated from appsettings.json
//      without any manual services.AddOptions<T>().BindConfiguration(...)
//      call in user code. Prior to the fix, this path silently returned a
//      default-constructed WeatherOptions.
//
//   3. UsingCurrentProcessCliArgs() forwards the current process's command
//      line arguments to the web application builder, enabling --urls,
//      --environment, and other CLI switches without manual plumbing.
// ─────────────────────────────────────────────────────────────────────────────

var webApplication = new Syringe()
    .UsingSourceGen()
    .ForWebApplication()
    .UsingOptions(() => CreateWebApplicationOptions.Default
        .UsingCurrentProcessCliArgs())
    .BuildWebApplication();
var webAppTask = webApplication.RunAsync();

var serviceProvider = webApplication.Services;

Console.WriteLine("MinimalWebApiSourceGen Example");
Console.WriteLine("==============================");
Console.WriteLine("Source-generated type discovery + source-gen [Options] binding.");
Console.WriteLine();

Console.WriteLine("─── 1. Source-gen injectable discovery ───");
Console.WriteLine(
    $"  serviceProvider.GetService<WeatherPlugin>():   {serviceProvider.GetService<WeatherPlugin>() is not null}");
Console.WriteLine(
    $"  serviceProvider.GetService<WeatherProvider>(): {serviceProvider.GetService<WeatherProvider>() is not null}");
Console.WriteLine(
    $"  serviceProvider.GetService<IConfiguration>():  {serviceProvider.GetService<IConfiguration>() is not null}");
Console.WriteLine();

Console.WriteLine("─── 2. Source-gen [Options] binding (web path) ───");
var weatherOptions = serviceProvider.GetRequiredService<IOptions<WeatherOptions>>().Value;
Console.WriteLine(
    $"  IOptions<WeatherOptions>.Value.Summary:            \"{weatherOptions.Summary}\"");
Console.WriteLine(
    $"  IOptions<WeatherOptions>.Value.TemperatureCelsius: {weatherOptions.TemperatureCelsius}");
Console.WriteLine();
Console.WriteLine("  These values came from appsettings.{Environment}.json via the");
Console.WriteLine("  source-generated TypeRegistry.RegisterOptions callback invoked by");
Console.WriteLine("  WebApplicationSyringe.BuildWebApplication(). No manual AddOptions");
Console.WriteLine("  or BindConfiguration call was needed in Program.cs or WeatherPlugin.");
Console.WriteLine();

if (string.IsNullOrEmpty(weatherOptions.Summary))
{
    Console.WriteLine(
        "  ⚠  Summary is empty — this is the bug signature. It indicates the");
    Console.WriteLine(
        "     source-gen options registrar was NOT invoked on the web path.");
    Environment.ExitCode = 1;
}
else
{
    Console.WriteLine(
        "  ✓  Summary is populated — the web path correctly invoked the");
    Console.WriteLine(
        "     source-generated AddOptions<WeatherOptions>().BindConfiguration(\"Weather\").");
}
Console.WriteLine();

Console.WriteLine("─── 3. Endpoint ───");
Console.WriteLine("  GET /weather  →  returns the bound WeatherOptions as JSON.");
Console.WriteLine();

await webAppTask;
