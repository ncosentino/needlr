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
Console.WriteLine();

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

// ─────────────────────────────────────────────────────────────────────────────
// INTERCEPTORS DEMO
// ─────────────────────────────────────────────────────────────────────────────
Console.WriteLine();
Console.WriteLine("═══════════════════════════════════════════════════════════════════════════════");
Console.WriteLine("INTERCEPTORS DEMO");
Console.WriteLine("═══════════════════════════════════════════════════════════════════════════════");
Console.WriteLine();

var dataService = provider.GetRequiredService<IDataService>();

// First call - cache MISS, shows timing
Console.WriteLine("── First call to GetData('user-123') ──");
var data1 = dataService.GetData("user-123");
Console.WriteLine($"Result: {data1}");
Console.WriteLine();

// Second call with same key - cache HIT (no timing because cache returns early)
Console.WriteLine("── Second call to GetData('user-123') - should be cached ──");
var data2 = dataService.GetData("user-123");
Console.WriteLine($"Result: {data2}");
Console.WriteLine();

// Different key - cache MISS
Console.WriteLine("── Call with different key GetData('user-456') ──");
var data3 = dataService.GetData("user-456");
Console.WriteLine($"Result: {data3}");
Console.WriteLine();

// Async method
Console.WriteLine("── Async method ComputeAsync(21) ──");
var computed = await dataService.ComputeAsync(21);
Console.WriteLine($"Result: {computed}");
Console.WriteLine();

// Void method
Console.WriteLine("── Void method LogMessage ──");
dataService.LogMessage("Hello from intercepted service!");
Console.WriteLine();

// ─────────────────────────────────────────────────────────────────────────────
// METHOD-LEVEL INTERCEPTORS DEMO
// ─────────────────────────────────────────────────────────────────────────────
Console.WriteLine("═══════════════════════════════════════════════════════════════════════════════");
Console.WriteLine("METHOD-LEVEL INTERCEPTORS DEMO");
Console.WriteLine("═══════════════════════════════════════════════════════════════════════════════");
Console.WriteLine();

var calculator = provider.GetRequiredService<ICalculatorService>();

// Add - no interceptor (fast, no logging)
Console.WriteLine("── Add(5, 3) - NO interceptor ──");
var sum = calculator.Add(5, 3);
Console.WriteLine($"Result: {sum}");
Console.WriteLine();

// Multiply - only TimingInterceptor
Console.WriteLine("── Multiply(7, 6) - TimingInterceptor only ──");
var product = calculator.Multiply(7, 6);
Console.WriteLine($"Result: {product}");
Console.WriteLine();

// Divide - both TimingInterceptor and CachingInterceptor
Console.WriteLine("── Divide(100, 5) - Timing + Caching ──");
var quotient1 = calculator.Divide(100, 5);
Console.WriteLine($"Result: {quotient1}");
Console.WriteLine();

Console.WriteLine("── Divide(100, 5) again - should be cached ──");
var quotient2 = calculator.Divide(100, 5);
Console.WriteLine($"Result: {quotient2}");
Console.WriteLine();
