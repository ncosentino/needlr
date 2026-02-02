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
/// Benchmarks comparing first/cold-start service resolution between source-gen and reflection.
/// Measures the cost of the very first resolution before any caching kicks in.
/// Critical for serverless, AOT, and cold-start scenarios.
/// Each iteration builds a fresh container to ensure cold resolution.
/// Manual DI is the baseline.
/// </summary>
[Config(typeof(BenchmarkConfig))]
public class FirstResolutionBenchmarks
{
    private IConfiguration _configuration = null!;
    private Assembly[] _assemblies = null!;
    private IServiceProvider? _lastProvider;

    [GlobalSetup]
    public void Setup()
    {
        _configuration = new ConfigurationBuilder().Build();
        _assemblies = [typeof(SimpleService1).Assembly];
    }

    [IterationCleanup]
    public void IterationCleanup()
    {
        (_lastProvider as IDisposable)?.Dispose();
        _lastProvider = null;
    }

    [Benchmark(Baseline = true)]
    public ISimpleService1 ManualDI_BuildAndResolveFirst()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ISimpleService1, SimpleService1>();
        _lastProvider = services.BuildServiceProvider();
        return _lastProvider.GetRequiredService<ISimpleService1>();
    }

    [Benchmark]
    public ISimpleService1 Needlr_Reflection_BuildAndResolveFirst()
    {
        _lastProvider = new Syringe()
            .UsingReflection()
            .UsingAdditionalAssemblies(_assemblies)
            .BuildServiceProvider(_configuration);
        return _lastProvider.GetRequiredService<ISimpleService1>();
    }

    [Benchmark]
    public ISimpleService1 Needlr_SourceGen_BuildAndResolveFirst()
    {
        _lastProvider = new Syringe()
            .UsingSourceGen()
            .UsingAdditionalAssemblies(_assemblies)
            .BuildServiceProvider(_configuration);
        return _lastProvider.GetRequiredService<ISimpleService1>();
    }
}
