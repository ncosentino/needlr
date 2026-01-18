using AotSourceGenConsolePlugin;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using NexusLabs.Needlr.Injection;
using NexusLabs.Needlr.Injection.SourceGen;

var config = new ConfigurationManager();
config["Greeting"] = "hello";
config["Weather:Prefix"] = "AOT";

var provider = new Syringe()
    .UsingSourceGen()
    .BuildServiceProvider(config);

Console.WriteLine("AotSourceGenConsoleApp running. Reflection disabled; demonstrating source-gen registry + plugins.");

var weather = provider.GetRequiredService<IConsoleWeatherProvider>();
var time = provider.GetRequiredService<IConsoleTimeProvider>();
var manual = provider.GetRequiredService<IConsoleManualService>();
var report = provider.GetRequiredService<IConsoleReport>();

Console.WriteLine($"weather: {weather.GetForecast()}");
Console.WriteLine($"time:    {time.GetNow():O}");
Console.WriteLine($"manual:  {manual.Echo("hi")}");
Console.WriteLine("report:");
Console.WriteLine(report.BuildReport());

Console.WriteLine($"NotInjectedService resolved? {provider.GetService<NotInjectedService>() is not null}");
Console.WriteLine($"Assemblies discovered: {provider.GetRequiredService<IReadOnlyList<System.Reflection.Assembly>>().Count}");
