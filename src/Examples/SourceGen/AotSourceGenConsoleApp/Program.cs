using AotSourceGenConsolePlugin;
using AotSourceGenConsolePlugin.Generated;

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

// ─────────────────────────────────────────────────────────────────────────────
// FACTORY DELEGATES DEMO
// ─────────────────────────────────────────────────────────────────────────────
Console.WriteLine("═══════════════════════════════════════════════════════════════════════════════");
Console.WriteLine("FACTORY DELEGATES DEMO");
Console.WriteLine("═══════════════════════════════════════════════════════════════════════════════");
Console.WriteLine();

// [GenerateFactory] creates both IFactory and Func<> for types with mixed params
// DatabaseConnection has injectable IConsoleTimeProvider + runtime connectionString

Console.WriteLine("── Using IDatabaseConnectionFactory ──");
var dbFactory = provider.GetRequiredService<AotSourceGenConsolePlugin.Generated.IDatabaseConnectionFactory>();
var prodDb = dbFactory.Create("Server=prod;Database=app");
var testDb = dbFactory.Create("Server=localhost;Database=test");

Console.WriteLine($"Prod query: {prodDb.Query("SELECT * FROM users")}");
Console.WriteLine($"Test query: {testDb.Query("SELECT * FROM orders")}");
Console.WriteLine();

Console.WriteLine("── Using Func<string, DatabaseConnection> ──");
var dbFunc = provider.GetRequiredService<Func<string, DatabaseConnection>>();
var devDb = dbFunc("Server=dev;Database=sandbox");
Console.WriteLine($"Dev query: {devDb.Query("SELECT 1")}");
Console.WriteLine();

// [GenerateFactory<IRequestHandler>] returns the interface type, enabling mocking
Console.WriteLine("── Using Func<Guid, IRequestHandler> (returns interface) ──");
var handlerFunc = provider.GetRequiredService<Func<Guid, IRequestHandler>>();
var correlationId = Guid.NewGuid();
IRequestHandler handler = handlerFunc(correlationId);
Console.WriteLine($"Handler type: {handler.GetType().Name}");
Console.WriteLine($"Handler result: {handler.Handle("Process payment")}");
Console.WriteLine();

var handlerFactory = provider.GetRequiredService<IRequestHandlerFactory>();
IRequestHandler requestHandler = handlerFactory.Create(Guid.NewGuid());

// Multiple constructors = multiple factory overloads
Console.WriteLine("── Using IReportGeneratorFactory with multiple Create overloads ──");
var reportFactory = provider.GetRequiredService<AotSourceGenConsolePlugin.Generated.IReportGeneratorFactory>();
var simpleReport = reportFactory.Create("Sales Summary");
var limitedReport = reportFactory.Create("Top Products", 10);
Console.WriteLine(simpleReport.Generate());
Console.WriteLine(limitedReport.Generate());
Console.WriteLine();

// ─────────────────────────────────────────────────────────────────────────────
// REGISTER AS DEMO
// ─────────────────────────────────────────────────────────────────────────────
Console.WriteLine("═══════════════════════════════════════════════════════════════════════════════");
Console.WriteLine("REGISTER AS DEMO");
Console.WriteLine("═══════════════════════════════════════════════════════════════════════════════");
Console.WriteLine();

// FileService implements IFileReader, IFileWriter, and IFileDeleter
// But with [RegisterAs<IFileReader>], only IFileReader is resolvable from DI

Console.WriteLine("── FileService registered only as IFileReader ──");
var fileReader = provider.GetService<IFileReader>();
var fileWriter = provider.GetService<IFileWriter>();
var fileDeleter = provider.GetService<IFileDeleter>();

Console.WriteLine($"IFileReader resolved: {fileReader is not null} ({fileReader?.GetType().Name ?? "null"})");
Console.WriteLine($"IFileWriter resolved: {fileWriter is not null}");
Console.WriteLine($"IFileDeleter resolved: {fileDeleter is not null}");

if (fileReader is not null)
{
    Console.WriteLine($"FileReader result: {fileReader.ReadFile("/data/config.json")}");
}

// The concrete type is still resolvable (always registered as self)
Console.WriteLine();
Console.WriteLine("── Concrete FileService is still resolvable ──");
var fileService = provider.GetRequiredService<FileService>();
Console.WriteLine($"Concrete type: {fileService.GetType().Name}");
Console.WriteLine($"Via concrete - Read: {fileService.ReadFile("/data/users.json")}");
fileService.WriteFile("/logs/app.log", "Application started");
Console.WriteLine();
