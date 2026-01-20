using BenchmarkDotNet.Attributes;

using Microsoft.Extensions.Configuration;

using NexusLabs.Needlr.Benchmarks.TestTypes;
using NexusLabs.Needlr.Injection;
using NexusLabs.Needlr.Injection.Reflection;
using NexusLabs.Needlr.Injection.SourceGen;

using System.Reflection;

namespace NexusLabs.Needlr.Benchmarks.Benchmarks;

/// <summary>
/// End-to-end benchmarks comparing full Syringe pipeline for source-gen vs reflection.
/// Uses REAL generated TypeRegistry from source generator.
/// Reflection is the baseline.
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
    public IServiceProvider BuildServiceProvider_Reflection()
    {
        return new Syringe()
            .UsingReflection()
            .UsingAdditionalAssemblies(_assemblies)
            .BuildServiceProvider(_configuration);
    }

    [Benchmark]
    public IServiceProvider BuildServiceProvider_SourceGenExplicit_AssemblyListProvided()
    {
        return new Syringe()
            .UsingGeneratedComponents(
                NexusLabs.Needlr.Benchmarks.Generated.TypeRegistry.GetInjectableTypes,
                NexusLabs.Needlr.Benchmarks.Generated.TypeRegistry.GetPluginTypes)
            .UsingAdditionalAssemblies(_assemblies)
            .BuildServiceProvider(_configuration);
    }

    [Benchmark]
    public IServiceProvider BuildServiceProvider_SourceGenImplicit_AssemblyListProvided()
    {
        return new Syringe()
            .UsingSourceGen()
            .UsingAdditionalAssemblies(_assemblies)
            .BuildServiceProvider(_configuration);
    }

    [Benchmark]
    public IServiceProvider BuildServiceProvider_SourceGenExplicit_NoAssemblyList()
    {
        return new Syringe()
            .UsingGeneratedComponents(
                NexusLabs.Needlr.Benchmarks.Generated.TypeRegistry.GetInjectableTypes,
                NexusLabs.Needlr.Benchmarks.Generated.TypeRegistry.GetPluginTypes)
            .BuildServiceProvider(_configuration);
    }

    [Benchmark]
    public IServiceProvider BuildServiceProvider_SourceGenImplicit_NoAssemblyList()
    {
        return new Syringe()
            .UsingSourceGen()
            .BuildServiceProvider(_configuration);
    }
}
