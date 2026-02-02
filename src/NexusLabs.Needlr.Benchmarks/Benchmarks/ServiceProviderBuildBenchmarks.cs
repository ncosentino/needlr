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
/// End-to-end benchmarks comparing full Syringe pipeline for source-gen vs reflection.
/// Uses REAL generated TypeRegistry from source generator.
/// Manual DI is the baseline.
/// </summary>
[Config(typeof(BenchmarkConfig))]
public class ServiceProviderBuildBenchmarks
{
    private IConfiguration _configuration = null!;
    private Assembly[] _assemblies = null!;

    [GlobalSetup]
    public void Setup()
    {
        _configuration = new ConfigurationBuilder().Build();
        _assemblies = [typeof(SimpleService1).Assembly];
    }

    [Benchmark(Baseline = true)]
    public IServiceProvider ManualDI_BuildServiceProvider()
    {
        var services = new ServiceCollection();
        // Register the same types that would be auto-discovered
        services.AddSingleton<ISimpleService1, SimpleService1>();
        services.AddSingleton<ISimpleService2, SimpleService2>();
        services.AddSingleton<ISimpleService3, SimpleService3>();
        services.AddSingleton<ISimpleService4, SimpleService4>();
        services.AddSingleton<ISimpleService5, SimpleService5>();
        return services.BuildServiceProvider();
    }

    [Benchmark]
    public IServiceProvider Needlr_Reflection_BuildServiceProvider()
    {
        return new Syringe()
            .UsingReflection()
            .UsingAdditionalAssemblies(_assemblies)
            .BuildServiceProvider(_configuration);
    }

    [Benchmark]
    public IServiceProvider Needlr_SourceGenExplicit_BuildServiceProvider()
    {
        return new Syringe()
            .UsingGeneratedComponents(
                NexusLabs.Needlr.Benchmarks.Generated.TypeRegistry.GetInjectableTypes,
                NexusLabs.Needlr.Benchmarks.Generated.TypeRegistry.GetPluginTypes)
            .UsingAdditionalAssemblies(_assemblies)
            .BuildServiceProvider(_configuration);
    }

    [Benchmark]
    public IServiceProvider Needlr_SourceGenImplicit_BuildServiceProvider()
    {
        return new Syringe()
            .UsingSourceGen()
            .UsingAdditionalAssemblies(_assemblies)
            .BuildServiceProvider(_configuration);
    }
}
