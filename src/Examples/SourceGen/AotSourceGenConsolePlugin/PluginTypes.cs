using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using NexusLabs.Needlr;
using NexusLabs.Needlr.Generators;

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection;

namespace AotSourceGenConsolePlugin;

// ═══════════════════════════════════════════════════════════════════════════════
// AOT-COMPATIBLE OPTIONS DEMO
// ═══════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Enum for testing AOT-compatible enum binding.
/// </summary>
public enum LogLevel
{
    Debug = 0,
    Info = 1,
    Warning = 2,
    Error = 3
}

/// <summary>
/// Simple options class for testing AOT-compatible configuration binding.
/// Uses primitive types: string, int, bool, double, and enum.
/// </summary>
[Options("Database")]
public class DatabaseOptions
{
    public string ConnectionString { get; set; } = "";
    public int CommandTimeout { get; set; } = 30;
    public bool EnableRetry { get; set; } = true;
    public double RetryDelaySeconds { get; set; } = 1.5;
    public LogLevel Level { get; set; } = LogLevel.Info;
}

/// <summary>
/// Named options for testing AOT-compatible named options binding.
/// </summary>
[Options("Logging", Name = "Verbose")]
public class LoggingOptions
{
    public string Path { get; set; } = "";
    public LogLevel MinLevel { get; set; } = LogLevel.Debug;
}

/// <summary>
/// Options with init-only properties for testing AOT factory pattern.
/// Init-only properties are bound in AOT using Options.Create with object initializers.
/// </summary>
[Options("Caching")]
public class CachingOptions
{
    public string Provider { get; init; } = "Memory";
    public int ExpirationMinutes { get; init; } = 60;
    public bool EnableCompression { get; init; } = false;
}

/// <summary>
/// Positional record options for testing AOT-compatible constructor binding.
/// Positional records are bound in AOT using Options.Create with constructor.
/// </summary>
[Options("Security")]
public partial record SecurityOptions(
    string SecretKey,
    bool RequireHttps,
    int TokenExpirySeconds
);

/// <summary>
/// Validated options for testing AOT-compatible validation.
/// Uses ValidateOnStart with DataAnnotations and a custom validator.
/// </summary>
[Options("Api", ValidateOnStart = true)]
public class ApiOptions
{
    [System.ComponentModel.DataAnnotations.Required]
    [System.ComponentModel.DataAnnotations.Url]
    public string Endpoint { get; set; } = "";
    
    [System.ComponentModel.DataAnnotations.Range(1, 300)]
    public int TimeoutSeconds { get; set; } = 30;
    
    public string ApiKey { get; set; } = "";
    
    public IEnumerable<string> Validate()
    {
        if (string.IsNullOrWhiteSpace(ApiKey))
            yield return "ApiKey is required";
    }
}

/// <summary>
/// Options with complex types for testing AOT-compatible complex binding.
/// Demonstrates nested objects, lists, and dictionaries.
/// </summary>
[Options("Cluster")]
public class ClusterOptions
{
    public string Name { get; set; } = "";
    public NodeConfig Primary { get; set; } = new();
    public List<string> Tags { get; set; } = new();
    public Dictionary<string, int> Limits { get; set; } = new();
}

/// <summary>
/// Nested configuration object for ClusterOptions.
/// </summary>
public class NodeConfig
{
    public string Host { get; set; } = "";
    public int Port { get; set; } = 8080;
}

public interface IConsoleWeatherProvider
{
    string GetForecast();
}

internal sealed class ConsoleWeatherProvider(IConfiguration config) : IConsoleWeatherProvider
{
    public string GetForecast()
    {
        var prefix = config["Weather:Prefix"] ?? "";
        return string.IsNullOrWhiteSpace(prefix)
            ? "Sunny with AOT"
            : $"{prefix}: Sunny with AOT";
    }
}

public interface IConsoleTimeProvider
{
    DateTimeOffset GetNow();
}

internal sealed class ConsoleTimeProvider : IConsoleTimeProvider
{
    public DateTimeOffset GetNow() => DateTimeOffset.UtcNow;
}

[DoNotAutoRegister]
public interface IConsoleManualService
{
    string Echo(string value);
}

internal sealed class ConsoleManualService : IConsoleManualService
{
    public string Echo(string value) => $"manual:{value}";
}

// Manual registration via IServiceCollectionPlugin (must run, even though IConsoleManualService is [DoNotAutoRegister]).
internal sealed class ConsoleManualRegistrationPlugin : IServiceCollectionPlugin
{
    public void Configure(ServiceCollectionPluginOptions options)
    {
        options.Services.AddSingleton<IConsoleManualService, ConsoleManualService>();
    }
}

public interface IConsoleReport
{
    string BuildReport();
}

internal sealed class ConsoleReport(
    IConsoleWeatherProvider weather,
    IConsoleTimeProvider time,
    Lazy<IConsoleManualService> manual,
    IReadOnlyList<Assembly> assemblies,
    IConfiguration config) : IConsoleReport
{
    public string BuildReport()
    {
        var greeting = config["Greeting"] ?? "(no greeting configured)";
        return string.Join(Environment.NewLine, new[]
        {
            $"greeting={greeting}",
            $"weather={weather.GetForecast()}",
            $"time={time.GetNow():O}",
            $"manual={manual.Value.Echo("from-report")}",
            $"assemblies={assemblies.Count}"
        });
    }
}

[DoNotInject]
public sealed class NotInjectedService;

// Post-build plugin for runtime verification.
internal sealed class ConsolePostBuildPlugin : IPostBuildServiceCollectionPlugin
{
    public void Configure(PostBuildServiceCollectionPluginOptions options)
    {
        var manual = options.Provider.GetRequiredService<IConsoleManualService>();
        Console.WriteLine($"ConsolePostBuildPlugin manual={manual.Echo("hi")}");

        var report = options.Provider.GetRequiredService<IConsoleReport>();
        Console.WriteLine("ConsolePostBuildPlugin report:\n" + report.BuildReport());
    }
}

/// <summary>
/// Interceptor that logs method entry/exit with timing information.
/// Unlike decorators, this single class works for ANY service and ANY method.
/// </summary>
public sealed class TimingInterceptor : IMethodInterceptor
{
    public async ValueTask<object?> InterceptAsync(IMethodInvocation invocation)
    {
        var typeName = invocation.Target.GetType().Name;
        var sw = Stopwatch.StartNew();
        Console.WriteLine($"[Timing] → {typeName}.{invocation.Method.Name}");
        
        try
        {
            var result = await invocation.ProceedAsync();
            Console.WriteLine($"[Timing] ← {typeName}.{invocation.Method.Name} ({sw.ElapsedMilliseconds}ms)");
            return result;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Timing] ✗ {typeName}.{invocation.Method.Name} FAILED ({sw.ElapsedMilliseconds}ms): {ex.Message}");
            throw;
        }
    }
}

/// <summary>
/// Interceptor that caches method results based on method name and arguments.
/// Demonstrates how interceptors can add caching to any service without modifying it.
/// </summary>
public sealed class CachingInterceptor : IMethodInterceptor
{
    private readonly ConcurrentDictionary<string, object?> _cache = new();

    public async ValueTask<object?> InterceptAsync(IMethodInvocation invocation)
    {
        var typeName = invocation.Target.GetType().Name;
        var args = invocation.Arguments.Length > 0 
            ? string.Join(",", invocation.Arguments.Select(a => a?.ToString() ?? "null"))
            : "";
        var cacheKey = $"{typeName}.{invocation.Method.Name}({args})";

        if (_cache.TryGetValue(cacheKey, out var cached))
        {
            Console.WriteLine($"[Cache] HIT: {cacheKey}");
            return cached;
        }

        Console.WriteLine($"[Cache] MISS: {cacheKey}");
        var result = await invocation.ProceedAsync();
        _cache[cacheKey] = result;
        return result;
    }
}
/// <summary>
/// Service interface for demonstrating interceptors.
/// </summary>
public interface IDataService
{
    string GetData(string key);
    Task<int> ComputeAsync(int value);
    void LogMessage(string message);
}

/// <summary>
/// Service with class-level interceptors applied.
/// TimingInterceptor (Order=1) runs first (outermost), CachingInterceptor (Order=2) runs second.
/// Call flow: Timing → Caching → actual method
/// </summary>
[Intercept<TimingInterceptor>(Order = 1)]
[Intercept<CachingInterceptor>(Order = 2)]
internal sealed class DataService : IDataService
{
    public string GetData(string key)
    {
        Thread.Sleep(50);
        return $"Data for '{key}' at {DateTime.Now:HH:mm:ss.fff}";
    }

    public async Task<int> ComputeAsync(int value)
    {
        await Task.Delay(100);
        return value * 2;
    }

    public void LogMessage(string message)
    {
        Console.WriteLine($"[DataService] {message}");
    }
}

/// <summary>
/// Service interface for demonstrating method-level interceptors.
/// </summary>
public interface ICalculatorService
{
    int Add(int a, int b);
    int Multiply(int a, int b);
    int Divide(int a, int b);
}

/// <summary>
/// Service with method-level interceptors - only specific methods are intercepted.
/// This demonstrates selective interception where timing is only applied to expensive operations.
/// </summary>
internal sealed class CalculatorService : ICalculatorService
{
    public int Add(int a, int b) => a + b;

    [Intercept<TimingInterceptor>]
    public int Multiply(int a, int b)
    {
        Thread.Sleep(30);
        return a * b;
    }

    [Intercept<TimingInterceptor>]
    [Intercept<CachingInterceptor>]
    public int Divide(int a, int b)
    {
        Thread.Sleep(20);
        return a / b;
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
// FACTORY DELEGATES DEMO
// ═══════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Interface for a database connection that requires a runtime connection string.
/// </summary>
public interface IDatabaseConnection
{
    string ConnectionString { get; }
    string Query(string sql);
}

/// <summary>
/// Database connection that requires both injectable dependencies (logger) and
/// a runtime parameter (connection string). Uses [GenerateFactory] to create a factory.
/// 
/// The generator creates:
/// - IDatabaseConnectionFactory with Create(string connectionString)
/// - Func&lt;string, DatabaseConnection&gt;
/// </summary>
[GenerateFactory]
public sealed class DatabaseConnection : IDatabaseConnection
{
    private readonly IConsoleTimeProvider _timeProvider;

    public string ConnectionString { get; }

    /// <summary>
    /// Creates a new database connection.
    /// </summary>
    /// <param name="timeProvider">The time provider for timestamps.</param>
    /// <param name="connectionString">The database connection string (e.g., "Server=localhost;Database=app").</param>
    public DatabaseConnection(IConsoleTimeProvider timeProvider, string connectionString)
    {
        _timeProvider = timeProvider;
        ConnectionString = connectionString;
    }

    public string Query(string sql)
    {
        return $"[{_timeProvider.GetNow():HH:mm:ss}] Executed on '{ConnectionString}': {sql}";
    }
}

/// <summary>
/// Interface for a request handler that processes requests with a correlation ID.
/// </summary>
public interface IRequestHandler
{
    Guid CorrelationId { get; }
    string Handle(string request);
}

/// <summary>
/// Request handler that uses [GenerateFactory&lt;IRequestHandler&gt;] so the factory
/// returns the interface type instead of the concrete class. This enables mocking
/// both the factory AND the returned instances in tests.
/// 
/// The generator creates:
/// - IRequestHandlerFactory with Create(Guid correlationId) returning IRequestHandler
/// - Func&lt;Guid, IRequestHandler&gt;
/// </summary>
[GenerateFactory<IRequestHandler>]
public sealed class RequestHandler : IRequestHandler
{
    private readonly IConfiguration _config;

    public Guid CorrelationId { get; }

    /// <summary>
    /// Creates a new request handler.
    /// </summary>
    /// <param name="config">The application configuration.</param>
    /// <param name="correlationId">Unique identifier for tracing this request across services.</param>
    public RequestHandler(IConfiguration config, Guid correlationId)
    {
        _config = config;
        CorrelationId = correlationId;
    }

    public string Handle(string request)
    {
        var prefix = _config["RequestPrefix"] ?? "REQ";
        return $"[{prefix}:{CorrelationId:N}] Handled: {request}";
    }
}

/// <summary>
/// Service with multiple constructors - each gets its own factory overload.
/// Demonstrates the flexibility of factory generation with constructor overloading.
/// </summary>
[GenerateFactory]
public sealed class ReportGenerator
{
    private readonly IConsoleTimeProvider _timeProvider;

    public string Title { get; }
    public int? MaxItems { get; }

    /// <summary>
    /// Creates a basic report generator.
    /// </summary>
    /// <param name="timeProvider">The time provider for timestamps.</param>
    /// <param name="title">The title to display at the top of the report.</param>
    public ReportGenerator(IConsoleTimeProvider timeProvider, string title)
    {
        _timeProvider = timeProvider;
        Title = title;
        MaxItems = null;
    }

    /// <summary>
    /// Creates a report generator with a maximum item limit.
    /// </summary>
    /// <param name="timeProvider">The time provider for timestamps.</param>
    /// <param name="title">The title to display at the top of the report.</param>
    /// <param name="maxItems">Maximum number of items to include in the report.</param>
    public ReportGenerator(IConsoleTimeProvider timeProvider, string title, int maxItems)
    {
        _timeProvider = timeProvider;
        Title = title;
        MaxItems = maxItems;
    }

    public string Generate()
    {
        var limit = MaxItems.HasValue ? $" (max {MaxItems})" : "";
        return $"Report '{Title}'{limit} generated at {_timeProvider.GetNow():HH:mm:ss}";
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
// REGISTER AS DEMO
// ═══════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Public interface for file reading operations.
/// </summary>
public interface IFileReader
{
    string ReadFile(string path);
}

/// <summary>
/// Internal interface for file writing - not exposed to consumers.
/// </summary>
public interface IFileWriter
{
    void WriteFile(string path, string content);
}

/// <summary>
/// Internal interface for file deletion - not exposed to consumers.
/// </summary>
public interface IFileDeleter
{
    void DeleteFile(string path);
}

/// <summary>
/// A comprehensive file service that implements multiple interfaces, but is
/// only publicly registered as IFileReader using [RegisterAs&lt;IFileReader&gt;].
/// 
/// This demonstrates how to:
/// - Implement multiple interfaces for internal use
/// - Only expose specific interfaces to DI consumers
/// - Control the public API surface of your services
/// </summary>
[RegisterAs<IFileReader>]
public sealed class FileService : IFileReader, IFileWriter, IFileDeleter
{
    private readonly IConsoleTimeProvider _timeProvider;

    public FileService(IConsoleTimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
    }

    public string ReadFile(string path) 
        => $"[{_timeProvider.GetNow():HH:mm:ss}] Read content from '{path}'";

    public void WriteFile(string path, string content) 
        => Console.WriteLine($"[{_timeProvider.GetNow():HH:mm:ss}] Wrote to '{path}': {content}");

    public void DeleteFile(string path) 
        => Console.WriteLine($"[{_timeProvider.GetNow():HH:mm:ss}] Deleted '{path}'");
}
