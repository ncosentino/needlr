using Microsoft.Extensions.DependencyInjection;

namespace NexusLabs.Needlr.IntegrationTests;

/// <summary>
/// Interface for payment processors, used to test keyed service injection.
/// </summary>
public interface IPaymentProcessor
{
    string Name { get; }
}

/// <summary>
/// Primary payment processor implementation.
/// </summary>
[DoNotAutoRegister] // Registered manually via keyed services
public sealed class PrimaryPaymentProcessor : IPaymentProcessor
{
    public string Name => "primary";
}

/// <summary>
/// Backup payment processor implementation.
/// </summary>
[DoNotAutoRegister] // Registered manually via keyed services
public sealed class BackupPaymentProcessor : IPaymentProcessor
{
    public string Name => "backup";
}

/// <summary>
/// Simple logger interface for testing mixed keyed/unkeyed dependencies.
/// </summary>
public interface ISimpleLogger
{
    void Log(string message);
}

/// <summary>
/// Simple logger implementation.
/// </summary>
public sealed class SimpleLogger : ISimpleLogger
{
    public void Log(string message) { }
}

/// <summary>
/// Service that depends on a keyed service via [FromKeyedServices].
/// </summary>
public sealed class ServiceWithKeyedDependency
{
    private readonly IPaymentProcessor _processor;

    public ServiceWithKeyedDependency(
        [FromKeyedServices("primary")] IPaymentProcessor processor)
    {
        _processor = processor;
    }

    public string GetPaymentProcessorName() => _processor.Name;
}

/// <summary>
/// Service with both keyed and unkeyed dependencies.
/// </summary>
public sealed class ServiceWithMixedDependencies
{
    private readonly IPaymentProcessor _processor;
    private readonly ISimpleLogger _logger;

    public ServiceWithMixedDependencies(
        [FromKeyedServices("backup")] IPaymentProcessor processor,
        ISimpleLogger logger)
    {
        _processor = processor;
        _logger = logger;
    }

    public string GetPaymentProcessorName() => _processor.Name;
    public ISimpleLogger GetLogger() => _logger;
}

/// <summary>
/// Service with multiple keyed dependencies.
/// </summary>
public sealed class ServiceWithMultipleKeyedDependencies
{
    private readonly IPaymentProcessor _primaryProcessor;
    private readonly IPaymentProcessor _backupProcessor;

    public ServiceWithMultipleKeyedDependencies(
        [FromKeyedServices("primary")] IPaymentProcessor primaryProcessor,
        [FromKeyedServices("backup")] IPaymentProcessor backupProcessor)
    {
        _primaryProcessor = primaryProcessor;
        _backupProcessor = backupProcessor;
    }

    public string GetPrimaryProcessorName() => _primaryProcessor.Name;
    public string GetBackupProcessorName() => _backupProcessor.Name;
}

/// <summary>
/// Plugin that registers keyed services for testing.
/// </summary>
public sealed class KeyedServicesRegistrationPlugin : IServiceCollectionPlugin
{
    public void Configure(ServiceCollectionPluginOptions options)
    {
        options.Services.AddKeyedSingleton<IPaymentProcessor, PrimaryPaymentProcessor>("primary");
        options.Services.AddKeyedSingleton<IPaymentProcessor, BackupPaymentProcessor>("backup");
    }
}

// ====== [Keyed] attribute test classes ======

/// <summary>
/// Interface for cache providers.
/// </summary>
public interface ICacheProvider
{
    string Name { get; }
}

/// <summary>
/// Redis cache implementation registered with [Keyed("redis")].
/// </summary>
[Keyed("redis")]
public sealed class RedisCacheProvider : ICacheProvider
{
    public string Name => "redis";
}

/// <summary>
/// Memory cache implementation registered with [Keyed("memory")].
/// </summary>
[Keyed("memory")]
public sealed class MemoryCacheProvider : ICacheProvider
{
    public string Name => "memory";
}

/// <summary>
/// Service that depends on a keyed cache provider via [FromKeyedServices].
/// </summary>
public sealed class ServiceWithKeyedCacheDependency
{
    private readonly ICacheProvider _cache;

    public ServiceWithKeyedCacheDependency(
        [FromKeyedServices("redis")] ICacheProvider cache)
    {
        _cache = cache;
    }

    public string GetCacheName() => _cache.Name;
}
