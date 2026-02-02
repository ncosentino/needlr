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
/// Benchmarks comparing service resolution with dependencies between source-gen and reflection.
/// All benchmarks measure resolution of a service with 3 dependencies.
/// Manual DI is the baseline.
/// </summary>
[Config(typeof(BenchmarkConfig))]
public class DependentServiceResolutionBenchmarks
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

        // Manual DI registration with dependencies
        var manualServices = new ServiceCollection();
        manualServices.AddSingleton<ISimpleService1, SimpleService1>();
        manualServices.AddSingleton<ISimpleService2, SimpleService2>();
        manualServices.AddSingleton<ISimpleService3, SimpleService3>();
        manualServices.AddSingleton<IDependentService11, DependentService11>();
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
    public IDependentService11 ManualDI_ResolveDependent()
    {
        return _manualProvider.GetRequiredService<IDependentService11>();
    }

    [Benchmark]
    public IDependentService11 Needlr_Reflection_ResolveDependent()
    {
        return _reflectionProvider.GetRequiredService<IDependentService11>();
    }

    [Benchmark]
    public IDependentService11 Needlr_SourceGen_ResolveDependent()
    {
        return _sourceGenProvider.GetRequiredService<IDependentService11>();
    }
}
