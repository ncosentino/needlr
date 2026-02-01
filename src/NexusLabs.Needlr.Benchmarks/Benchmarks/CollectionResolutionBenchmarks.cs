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
/// Reflection is the baseline.
/// </summary>
[Config(typeof(BenchmarkConfig))]
public class CollectionResolutionBenchmarks
{
    private IServiceProvider _reflectionProvider = null!;
    private IServiceProvider _sourceGenProvider = null!;
    private IConfiguration _configuration = null!;
    private Assembly[] _assemblies = null!;

    [GlobalSetup]
    public void Setup()
    {
        _configuration = new ConfigurationBuilder().Build();
        _assemblies = [typeof(SimpleService1).Assembly];

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
        (_reflectionProvider as IDisposable)?.Dispose();
        (_sourceGenProvider as IDisposable)?.Dispose();
    }

    [Benchmark(Baseline = true)]
    public IMultiInterface1A[] ResolveCollection_Reflection()
    {
        return _reflectionProvider.GetServices<IMultiInterface1A>().ToArray();
    }

    [Benchmark]
    public IMultiInterface1A[] ResolveCollection_SourceGen()
    {
        return _sourceGenProvider.GetServices<IMultiInterface1A>().ToArray();
    }
}
