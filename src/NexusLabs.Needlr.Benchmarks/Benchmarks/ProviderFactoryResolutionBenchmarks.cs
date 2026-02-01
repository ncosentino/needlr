using BenchmarkDotNet.Attributes;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using NexusLabs.Needlr.Benchmarks.TestTypes;
using NexusLabs.Needlr.Injection;
using NexusLabs.Needlr.Injection.SourceGen;

namespace NexusLabs.Needlr.Benchmarks.Benchmarks;

/// <summary>
/// Benchmarks comparing factory resolution through providers.
/// Measures the time to invoke a factory method from different sources.
/// </summary>
[Config(typeof(BenchmarkConfig))]
public class ProviderFactoryResolutionBenchmarks
{
    private IServiceProvider _baselineProvider = null!;
    private IServiceProvider _sourceGenProvider = null!;
    private Func<string, IProviderFactoryService> _baselineFunc = null!;
    private NexusLabs.Needlr.Benchmarks.Generated.IProviderFactoryServiceFactory _preResolvedFactory = null!;
    private IFactoryShorthandBenchmarkProvider _factoryShorthandProvider = null!;
    private IFactoryInterfaceProvider _factoryInterfaceProvider = null!;
    private IMixedShorthandProvider _mixedShorthandProvider = null!;
    private IConfiguration _configuration = null!;

    [GlobalSetup]
    public void Setup()
    {
        _configuration = new ConfigurationBuilder().Build();

        // Baseline: manual factory func registration with dependency
        var baselineServices = new ServiceCollection();
        baselineServices.AddSingleton<IProviderDependency1, ProviderDependency1>();
        baselineServices.AddSingleton<Func<string, IProviderFactoryService>>(sp =>
        {
            var dep = sp.GetRequiredService<IProviderDependency1>();
            return createdBy => new ProviderFactoryService(dep, createdBy);
        });
        _baselineProvider = baselineServices.BuildServiceProvider();
        _baselineFunc = _baselineProvider.GetRequiredService<Func<string, IProviderFactoryService>>();

        // Needlr source gen (includes factory generation)
        _sourceGenProvider = new Syringe()
            .UsingSourceGen()
            .BuildServiceProvider(_configuration);

        // Pre-resolve the direct factory
        _preResolvedFactory = _sourceGenProvider.GetRequiredService<NexusLabs.Needlr.Benchmarks.Generated.IProviderFactoryServiceFactory>();

        // Get providers via their generated interfaces
        _factoryShorthandProvider = _sourceGenProvider.GetRequiredService<IFactoryShorthandBenchmarkProvider>();
        _factoryInterfaceProvider = _sourceGenProvider.GetRequiredService<IFactoryInterfaceProvider>();
        _mixedShorthandProvider = _sourceGenProvider.GetRequiredService<IMixedShorthandProvider>();
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        (_baselineProvider as IDisposable)?.Dispose();
        (_sourceGenProvider as IDisposable)?.Dispose();
    }

    [Benchmark(Baseline = true)]
    public IProviderFactoryService ManualDI_FuncFactory_PreResolved()
    {
        return _baselineFunc("manual");
    }

    [Benchmark]
    public IProviderFactoryService ManualDI_FuncFactory_WithResolution()
    {
        var factory = _baselineProvider.GetRequiredService<Func<string, IProviderFactoryService>>();
        return factory("manual");
    }

    [Benchmark]
    public IProviderFactoryService DirectFactory_PreResolved()
    {
        return _preResolvedFactory.Create("direct");
    }

    [Benchmark]
    public IProviderFactoryService DirectFactory_WithResolution()
    {
        var factory = _sourceGenProvider.GetRequiredService<NexusLabs.Needlr.Benchmarks.Generated.IProviderFactoryServiceFactory>();
        return factory.Create("direct");
    }

    [Benchmark]
    public IProviderFactoryService Provider_FactoryShorthand()
    {
        return _factoryShorthandProvider.ProviderFactoryServiceFactory.Create("shorthand");
    }

    [Benchmark]
    public IProviderFactoryService Provider_FactoryInterface()
    {
        return _factoryInterfaceProvider.ProviderFactoryServiceFactory.Create("interface");
    }

    [Benchmark]
    public IProviderFactoryService Provider_MixedShorthand()
    {
        return _mixedShorthandProvider.ProviderFactoryServiceFactory.Create("mixed");
    }
}
