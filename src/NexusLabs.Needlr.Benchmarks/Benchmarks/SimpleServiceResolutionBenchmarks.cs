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
/// Benchmarks comparing simple service resolution between source-gen and reflection.
/// All benchmarks measure resolution of a simple service (no dependencies).
/// Manual DI is the baseline.
/// </summary>
[Config(typeof(BenchmarkConfig))]
public class SimpleServiceResolutionBenchmarks
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
        manualServices.AddSingleton<ISimpleService1, SimpleService1>();
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
    public ISimpleService1 ManualDI_ResolveSimple()
    {
        return _manualProvider.GetRequiredService<ISimpleService1>();
    }

    [Benchmark]
    public ISimpleService1 Needlr_Reflection_ResolveSimple()
    {
        return _reflectionProvider.GetRequiredService<ISimpleService1>();
    }

    [Benchmark]
    public ISimpleService1 Needlr_SourceGen_ResolveSimple()
    {
        return _sourceGenProvider.GetRequiredService<ISimpleService1>();
    }
}
