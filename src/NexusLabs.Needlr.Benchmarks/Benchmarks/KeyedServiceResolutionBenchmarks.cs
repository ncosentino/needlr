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
/// Benchmarks comparing keyed service resolution between source-gen and reflection.
/// All benchmarks measure resolution of a keyed service.
/// Manual DI is the baseline.
/// </summary>
[Config(typeof(BenchmarkConfig))]
public class KeyedServiceResolutionBenchmarks
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

        // Manual DI registration with keyed services
        var manualServices = new ServiceCollection();
        manualServices.AddKeyedSingleton<IKeyedService, PrimaryKeyedService>("primary");
        manualServices.AddKeyedSingleton<IKeyedService, SecondaryKeyedService>("secondary");
        manualServices.AddKeyedSingleton<IKeyedService, TertiaryKeyedService>("tertiary");
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
    public IKeyedService ManualDI_ResolveKeyed()
    {
        return _manualProvider.GetRequiredKeyedService<IKeyedService>("primary");
    }

    [Benchmark]
    public IKeyedService Needlr_Reflection_ResolveKeyed()
    {
        return _reflectionProvider.GetRequiredKeyedService<IKeyedService>("primary");
    }

    [Benchmark]
    public IKeyedService Needlr_SourceGen_ResolveKeyed()
    {
        return _sourceGenProvider.GetRequiredKeyedService<IKeyedService>("primary");
    }
}
