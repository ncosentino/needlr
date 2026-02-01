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
/// Reflection is the baseline.
/// </summary>
[Config(typeof(BenchmarkConfig))]
public class SimpleServiceResolutionBenchmarks
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
    public ISimpleService1 ResolveSimple_Reflection()
    {
        return _reflectionProvider.GetRequiredService<ISimpleService1>();
    }

    [Benchmark]
    public ISimpleService1 ResolveSimple_SourceGen()
    {
        return _sourceGenProvider.GetRequiredService<ISimpleService1>();
    }
}

/// <summary>
/// Benchmarks comparing service resolution with dependencies between source-gen and reflection.
/// All benchmarks measure resolution of a service with 3 dependencies.
/// Reflection is the baseline.
/// </summary>
[Config(typeof(BenchmarkConfig))]
public class DependentServiceResolutionBenchmarks
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
    public IDependentService11 ResolveDependent_Reflection()
    {
        return _reflectionProvider.GetRequiredService<IDependentService11>();
    }

    [Benchmark]
    public IDependentService11 ResolveDependent_SourceGen()
    {
        return _sourceGenProvider.GetRequiredService<IDependentService11>();
    }
}
