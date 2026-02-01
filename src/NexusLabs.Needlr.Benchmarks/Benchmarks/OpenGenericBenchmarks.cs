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
/// Benchmarks comparing open generic service resolution between source-gen and reflection.
/// Tests the IRepository&lt;T&gt; pattern common in enterprise applications.
/// Measures the cost of resolving closed generic types from open generic registrations.
/// Uses pre-registration callbacks to register open generics before auto-discovery.
/// Reflection is the baseline.
/// </summary>
[Config(typeof(BenchmarkConfig))]
public class OpenGenericBenchmarks
{
    private IServiceProvider _reflectionProvider = null!;
    private IServiceProvider _sourceGenProvider = null!;

    [GlobalSetup]
    public void Setup()
    {
        var configuration = new ConfigurationBuilder().Build();
        Assembly[] assemblies = [typeof(Repository<>).Assembly];

        // Build with reflection path + open generic via pre-registration
        _reflectionProvider = new Syringe()
            .UsingReflection()
            .UsingAdditionalAssemblies(assemblies)
            .UsingPreRegistrationCallback(services =>
                services.AddTransient(typeof(IRepository<>), typeof(Repository<>)))
            .BuildServiceProvider(configuration);

        // Build with source-gen path + open generic via pre-registration
        _sourceGenProvider = new Syringe()
            .UsingSourceGen()
            .UsingAdditionalAssemblies(assemblies)
            .UsingPreRegistrationCallback(services =>
                services.AddTransient(typeof(IRepository<>), typeof(Repository<>)))
            .BuildServiceProvider(configuration);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        (_reflectionProvider as IDisposable)?.Dispose();
        (_sourceGenProvider as IDisposable)?.Dispose();
    }

    [Benchmark(Baseline = true)]
    public IRepository<EntityA> ResolveOpenGeneric_Reflection()
    {
        return _reflectionProvider.GetRequiredService<IRepository<EntityA>>();
    }

    [Benchmark]
    public IRepository<EntityA> ResolveOpenGeneric_SourceGen()
    {
        return _sourceGenProvider.GetRequiredService<IRepository<EntityA>>();
    }
}
