using Microsoft.CodeAnalysis;

using Xunit;

namespace NexusLabs.Needlr.Generators.Tests;

/// <summary>
/// Tests for NDLRGEN022: Disposable captive dependency detection.
/// Unlike NDLRCOR012 which only works with explicit lifetime attributes,
/// NDLRGEN022 uses inferred lifetimes from Needlr's convention-based discovery.
/// </summary>
public sealed class DisposableCaptiveDependencyDiagnosticTests
{
    [Fact]
    public void Generator_DetectsSingletonCapturingScopedDisposable()
    {
        var source = @"
using System;
using NexusLabs.Needlr.Generators;

[assembly: GenerateTypeRegistry]

namespace TestApp
{
    public interface IDbContext : IDisposable { }

    [NexusLabs.Needlr.Scoped]
    public class DbContext : IDbContext
    {
        public void Dispose() { }
    }

    [NexusLabs.Needlr.Singleton]
    public class CacheService
    {
        private readonly IDbContext _context;
        public CacheService(IDbContext context) => _context = context;
    }
}";

        var diagnostics = GeneratorTestRunner.ForTypeRegistry()
            .WithSource(source)
            .RunTypeRegistryGeneratorDiagnostics();

        var ndlrgen022 = diagnostics.Where(d => d.Id == "NDLRGEN022").ToList();
        Assert.Single(ndlrgen022);
        Assert.Contains("CacheService", ndlrgen022[0].GetMessage());
        Assert.Contains("Singleton", ndlrgen022[0].GetMessage());
    }

    [Fact]
    public void Generator_DetectsSingletonCapturingTransientDisposable()
    {
        var source = @"
using System;
using NexusLabs.Needlr.Generators;

[assembly: GenerateTypeRegistry]

namespace TestApp
{
    [NexusLabs.Needlr.Transient]
    public class DisposableService : IDisposable
    {
        public void Dispose() { }
    }

    [NexusLabs.Needlr.Singleton]
    public class LongLivedService
    {
        private readonly DisposableService _service;
        public LongLivedService(DisposableService service) => _service = service;
    }
}";

        var diagnostics = GeneratorTestRunner.ForTypeRegistry()
            .WithSource(source)
            .RunTypeRegistryGeneratorDiagnostics();

        var ndlrgen022 = diagnostics.Where(d => d.Id == "NDLRGEN022").ToList();
        Assert.Single(ndlrgen022);
        Assert.Contains("LongLivedService", ndlrgen022[0].GetMessage());
    }

    [Fact]
    public void Generator_DetectsScopedCapturingTransientDisposable()
    {
        var source = @"
using System;
using NexusLabs.Needlr.Generators;

[assembly: GenerateTypeRegistry]

namespace TestApp
{
    [NexusLabs.Needlr.Transient]
    public class TransientDisposable : IDisposable
    {
        public void Dispose() { }
    }

    [NexusLabs.Needlr.Scoped]
    public class ScopedService
    {
        private readonly TransientDisposable _disposable;
        public ScopedService(TransientDisposable disposable) => _disposable = disposable;
    }
}";

        var diagnostics = GeneratorTestRunner.ForTypeRegistry()
            .WithSource(source)
            .RunTypeRegistryGeneratorDiagnostics();

        var ndlrgen022 = diagnostics.Where(d => d.Id == "NDLRGEN022").ToList();
        Assert.Single(ndlrgen022);
        Assert.Contains("ScopedService", ndlrgen022[0].GetMessage());
        Assert.Contains("Scoped", ndlrgen022[0].GetMessage());
        Assert.Contains("Transient", ndlrgen022[0].GetMessage());
    }

    [Fact]
    public void Generator_NoWarning_WhenUsingFuncFactory()
    {
        var source = @"
using System;
using NexusLabs.Needlr.Generators;

[assembly: GenerateTypeRegistry]

namespace TestApp
{
    public class DisposableService : IDisposable
    {
        public void Dispose() { }
    }

    [NexusLabs.Needlr.Singleton]
    public class SafeService
    {
        private readonly Func<DisposableService> _factory;
        public SafeService(Func<DisposableService> factory) => _factory = factory;
    }
}";

        var diagnostics = GeneratorTestRunner.ForTypeRegistry()
            .WithSource(source)
            .RunTypeRegistryGeneratorDiagnostics();

        var ndlrgen022 = diagnostics.Where(d => d.Id == "NDLRGEN022").ToList();
        Assert.Empty(ndlrgen022);
    }

    [Fact]
    public void Generator_NoWarning_WhenUsingIServiceScopeFactory()
    {
        // This test verifies that factory patterns are skipped
        // IServiceScopeFactory has a specific pattern check
        var source = @"
using System;
using NexusLabs.Needlr.Generators;

[assembly: GenerateTypeRegistry]

namespace Microsoft.Extensions.DependencyInjection
{
    public interface IServiceScopeFactory { }
}

namespace TestApp
{
    [NexusLabs.Needlr.Singleton]
    public class SafeService
    {
        private readonly Microsoft.Extensions.DependencyInjection.IServiceScopeFactory _scopeFactory;
        public SafeService(Microsoft.Extensions.DependencyInjection.IServiceScopeFactory scopeFactory) => _scopeFactory = scopeFactory;
    }
}";

        var diagnostics = GeneratorTestRunner.ForTypeRegistry()
            .WithSource(source)
            .RunTypeRegistryGeneratorDiagnostics();

        var ndlrgen022 = diagnostics.Where(d => d.Id == "NDLRGEN022").ToList();
        Assert.Empty(ndlrgen022);
    }

    [Fact]
    public void Generator_NoWarning_WhenDisposableIsSameOrLongerLifetime()
    {
        var source = @"
using System;
using NexusLabs.Needlr.Generators;

[assembly: GenerateTypeRegistry]

namespace TestApp
{
    [NexusLabs.Needlr.Singleton]
    public class SingletonDisposable : IDisposable
    {
        public void Dispose() { }
    }

    [NexusLabs.Needlr.Singleton]
    public class SingletonService
    {
        private readonly SingletonDisposable _disposable;
        public SingletonService(SingletonDisposable disposable) => _disposable = disposable;
    }

    [NexusLabs.Needlr.Scoped]
    public class ScopedService
    {
        private readonly SingletonDisposable _disposable;
        public ScopedService(SingletonDisposable disposable) => _disposable = disposable;
    }
}";

        var diagnostics = GeneratorTestRunner.ForTypeRegistry()
            .WithSource(source)
            .RunTypeRegistryGeneratorDiagnostics();

        var ndlrgen022 = diagnostics.Where(d => d.Id == "NDLRGEN022").ToList();
        Assert.Empty(ndlrgen022);
    }

    [Fact]
    public void Generator_NoWarning_WhenDependencyIsNotDisposable()
    {
        var source = @"
using NexusLabs.Needlr.Generators;

[assembly: GenerateTypeRegistry]

namespace TestApp
{
    [NexusLabs.Needlr.Scoped]
    public class ScopedService
    {
    }

    [NexusLabs.Needlr.Singleton]
    public class SingletonService
    {
        private readonly ScopedService _scoped;
        public SingletonService(ScopedService scoped) => _scoped = scoped;
    }
}";

        var diagnostics = GeneratorTestRunner.ForTypeRegistry()
            .WithSource(source)
            .RunTypeRegistryGeneratorDiagnostics();

        // This would be a captive dependency but ScopedService is NOT disposable
        // so the diagnostic should NOT fire (no ObjectDisposedException risk)
        var ndlrgen022 = diagnostics.Where(d => d.Id == "NDLRGEN022").ToList();
        Assert.Empty(ndlrgen022);
    }

    [Fact]
    public void Generator_DetectsAsyncDisposable()
    {
        var source = @"
using System;
using NexusLabs.Needlr.Generators;

[assembly: GenerateTypeRegistry]

namespace TestApp
{
    [NexusLabs.Needlr.Scoped]
    public class AsyncDisposableService : IAsyncDisposable
    {
        public System.Threading.Tasks.ValueTask DisposeAsync() => default;
    }

    [NexusLabs.Needlr.Singleton]
    public class SingletonService
    {
        private readonly AsyncDisposableService _service;
        public SingletonService(AsyncDisposableService service) => _service = service;
    }
}";

        var diagnostics = GeneratorTestRunner.ForTypeRegistry()
            .WithSource(source)
            .RunTypeRegistryGeneratorDiagnostics();

        var ndlrgen022 = diagnostics.Where(d => d.Id == "NDLRGEN022").ToList();
        Assert.Single(ndlrgen022);
        Assert.Contains("AsyncDisposableService", ndlrgen022[0].GetMessage());
    }

    [Fact]
    public void Generator_DetectsHostedServiceCapturingDisposable()
    {
        // Use inline definitions for consistency with hosted service tests
        var source = """
            using System;
            using System.Threading;
            using System.Threading.Tasks;
            using Microsoft.Extensions.Hosting;
            using NexusLabs.Needlr;
            using NexusLabs.Needlr.Generators;

            [assembly: GenerateTypeRegistry(IncludeNamespacePrefixes = new[] { "TestApp" })]

            namespace TestApp
            {
                [Scoped]
                public class ScopedDisposable : IDisposable
                {
                    public void Dispose() { }
                }

                public class MyWorker : BackgroundService
                {
                    private readonly ScopedDisposable _scoped;
                    public MyWorker(ScopedDisposable scoped) => _scoped = scoped;
                    protected override Task ExecuteAsync(CancellationToken stoppingToken) => Task.CompletedTask;
                }
            }
            """;

        var diagnostics = GeneratorTestRunner.ForHostedServiceWithLifetimes()
            .WithSource(source)
            .RunTypeRegistryGeneratorDiagnostics();

        var ndlrgen022 = diagnostics.Where(d => d.Id == "NDLRGEN022").ToList();
        Assert.Single(ndlrgen022);
        Assert.Contains("MyWorker", ndlrgen022[0].GetMessage());
    }

    [Fact]
    public void Generator_UsesInferredLifetimes_WhenNoExplicitAttributes()
    {
        // This is the key differentiator from NDLRCOR012:
        // NDLRGEN022 works with inferred lifetimes, not just explicit attributes
        var source = @"
using System;
using NexusLabs.Needlr.Generators;

[assembly: GenerateTypeRegistry]

namespace TestApp
{
    public interface ILogger { void Log(string msg); }
    public class Logger : ILogger, IDisposable
    {
        public void Log(string msg) { }
        public void Dispose() { }
    }

    // Inferred lifetime: Logger is Singleton (parameterless or all-injectable ctor)
    // Inferred lifetime: Consumer is also Singleton
    // This should NOT trigger - both are Singleton
    public class Consumer
    {
        private readonly ILogger _logger;
        public Consumer(ILogger logger) => _logger = logger;
    }
}";

        var diagnostics = GeneratorTestRunner.ForTypeRegistry()
            .WithSource(source)
            .RunTypeRegistryGeneratorDiagnostics();

        var ndlrgen022 = diagnostics.Where(d => d.Id == "NDLRGEN022").ToList();
        Assert.Empty(ndlrgen022);
    }

    [Fact]
    public void Generator_NoWarning_ForTransientConsumer()
    {
        // Transient services can capture any lifetime - they're created fresh each time
        var source = @"
using System;
using NexusLabs.Needlr.Generators;

[assembly: GenerateTypeRegistry]

namespace TestApp
{
    [NexusLabs.Needlr.Scoped]
    public class ScopedDisposable : IDisposable
    {
        public void Dispose() { }
    }

    [NexusLabs.Needlr.Transient]
    public class TransientConsumer
    {
        private readonly ScopedDisposable _scoped;
        public TransientConsumer(ScopedDisposable scoped) => _scoped = scoped;
    }
}";

        var diagnostics = GeneratorTestRunner.ForTypeRegistry()
            .WithSource(source)
            .RunTypeRegistryGeneratorDiagnostics();

        var ndlrgen022 = diagnostics.Where(d => d.Id == "NDLRGEN022").ToList();
        Assert.Empty(ndlrgen022);
    }
}
