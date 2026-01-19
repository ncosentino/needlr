using BenchmarkDotNet.Attributes;

using Microsoft.Extensions.DependencyInjection;

using NexusLabs.Needlr.Benchmarks.TestTypes;
using NexusLabs.Needlr.Injection;
using NexusLabs.Needlr.Injection.Reflection.TypeFilterers;
using NexusLabs.Needlr.Injection.Reflection.TypeRegistrars;
using NexusLabs.Needlr.Injection.SourceGen.TypeFilterers;
using NexusLabs.Needlr.Injection.SourceGen.TypeRegistrars;

using System.Reflection;

namespace NexusLabs.Needlr.Benchmarks.Benchmarks;

/// <summary>
/// Benchmarks comparing GeneratedTypeRegistrar vs ReflectionTypeRegistrar.
/// Uses REAL generated TypeRegistry from source generator.
/// Reflection is the baseline.
/// </summary>
[Config(typeof(BenchmarkConfig))]
public class TypeRegistrationBenchmarks
{
    private Assembly[] _assemblies = null!;
    private ITypeFilterer _reflectionTypeFilterer = null!;
    private ITypeFilterer _sourceGenTypeFilterer = null!;
    private ReflectionTypeRegistrar _reflectionRegistrar = null!;
    private GeneratedTypeRegistrar _sourceGenRegistrar = null!;

    [GlobalSetup]
    public void Setup()
    {
        _assemblies = [typeof(SimpleService1).Assembly];
        _reflectionTypeFilterer = new ReflectionTypeFilterer();
        _sourceGenTypeFilterer = new GeneratedTypeFilterer(
            NexusLabs.Needlr.Generated.TypeRegistry.GetInjectableTypes);
        _reflectionRegistrar = new ReflectionTypeRegistrar();
        _sourceGenRegistrar = new GeneratedTypeRegistrar(
            NexusLabs.Needlr.Generated.TypeRegistry.GetInjectableTypes);
    }

    [Benchmark(Baseline = true)]
    public IServiceCollection RegisterTypes_Reflection()
    {
        var services = new ServiceCollection();
        _reflectionRegistrar.RegisterTypesFromAssemblies(services, _reflectionTypeFilterer, _assemblies);
        return services;
    }

    [Benchmark]
    public IServiceCollection RegisterTypes_SourceGen()
    {
        var services = new ServiceCollection();
        _sourceGenRegistrar.RegisterTypesFromAssemblies(services, _sourceGenTypeFilterer, _assemblies);
        return services;
    }
}
