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
/// Benchmarks comparing resolution of services with deep dependency chains.
/// Tests a 10-level deep dependency graph where each level depends on the previous.
/// Real applications often have complex dependency graphs; this measures scaling behavior.
/// Reflection is the baseline.
/// </summary>
[Config(typeof(BenchmarkConfig))]
public class DeepDependencyChainBenchmarks
{
    private IServiceProvider _reflectionProvider = null!;
    private IServiceProvider _sourceGenProvider = null!;
    private IConfiguration _configuration = null!;
    private Assembly[] _assemblies = null!;

    [GlobalSetup]
    public void Setup()
    {
        _configuration = new ConfigurationBuilder().Build();
        _assemblies = [typeof(DeepLevel1).Assembly];

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
    public IDeepLevel10 ResolveDeepChain_Reflection()
    {
        return _reflectionProvider.GetRequiredService<IDeepLevel10>();
    }

    [Benchmark]
    public IDeepLevel10 ResolveDeepChain_SourceGen()
    {
        return _sourceGenProvider.GetRequiredService<IDeepLevel10>();
    }
}
