using BenchmarkDotNet.Attributes;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using NexusLabs.Needlr.Benchmarks.TestTypes;
using NexusLabs.Needlr.Injection;
using NexusLabs.Needlr.Injection.Reflection;
using NexusLabs.Needlr.Injection.SourceGen;

using System.Reflection;

namespace NexusLabs.Needlr.Benchmarks.Benchmarks;

/// <summary>
/// Benchmarks comparing IEnumerable&lt;T&gt; collection resolution between source-gen and reflection.
/// Measures the cost of resolving all implementations of an interface.
/// Common pattern for plugin systems, handlers, and strategy patterns.
/// Manual DI is the baseline.
/// </summary>
[Config(typeof(BenchmarkConfig))]
public class CollectionResolutionBenchmarks
{
    private IServiceProvider _manualProvider = null!;
    private IServiceProvider _reflectionProvider = null!;
    private IServiceProvider _sourceGenProvider = null!;
    private IConfiguration _configuration = null!;
    private Assembly[] _assemblies = null!;

    [GlobalSetup]
    public void Setup()
    {
        _configuration = new ConfigurationBuilder().Build();
        _assemblies = [typeof(SimpleService1).Assembly];

        // Manual DI registration
        var manualServices = new ServiceCollection();
        manualServices.AddSingleton<IMultiInterface1A, MultiInterfaceService1>();
        manualServices.AddSingleton<IMultiInterface1A, MultiInterfaceService6>();
        manualServices.AddSingleton<IMultiInterface1A, MultiInterfaceService10>();
        manualServices.AddSingleton<ISimpleService1, SimpleService1>();
        manualServices.AddSingleton<ISimpleService5, SimpleService5>();
        _manualProvider = manualServices.BuildServiceProvider();

        _reflectionProvider = new Syringe()
            .UsingReflection()
            .UsingAdditionalAssemblies(_assemblies)
            .BuildServiceProvider(_configuration);

        _sourceGenProvider = new Syringe()
            .UsingSourceGen()
            .UsingAdditionalAssemblies(_assemblies)
            .BuildServiceProvider(_configuration);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        (_manualProvider as IDisposable)?.Dispose();
        (_reflectionProvider as IDisposable)?.Dispose();
        (_sourceGenProvider as IDisposable)?.Dispose();
    }

    [Benchmark(Baseline = true)]
    public IMultiInterface1A[] ManualDI_ResolveCollection()
    {
        return _manualProvider.GetServices<IMultiInterface1A>().ToArray();
    }

    [Benchmark]
    public IMultiInterface1A[] Needlr_Reflection_ResolveCollection()
    {
        return _reflectionProvider.GetServices<IMultiInterface1A>().ToArray();
    }

    [Benchmark]
    public IMultiInterface1A[] Needlr_SourceGen_ResolveCollection()
    {
        return _sourceGenProvider.GetServices<IMultiInterface1A>().ToArray();
    }
}
