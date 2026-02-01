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
/// Benchmarks comparing scoped service resolution between source-gen and reflection.
/// Measures the cost of creating a scope and resolving scoped services within it.
/// This is critical for web applications where each request creates a new scope.
/// Reflection is the baseline.
/// </summary>
[Config(typeof(BenchmarkConfig))]
public class ScopedServiceResolutionBenchmarks
{
    private IServiceProvider _reflectionProvider = null!;
    private IServiceProvider _sourceGenProvider = null!;
    private IConfiguration _configuration = null!;
    private Assembly[] _assemblies = null!;

    [GlobalSetup]
    public void Setup()
    {
        _configuration = new ConfigurationBuilder().Build();
        _assemblies = [typeof(ScopedService1).Assembly];

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
    public IScopedService3 CreateScopeAndResolve_Reflection()
    {
        using var scope = _reflectionProvider.CreateScope();
        return scope.ServiceProvider.GetRequiredService<IScopedService3>();
    }

    [Benchmark]
    public IScopedService3 CreateScopeAndResolve_SourceGen()
    {
        using var scope = _sourceGenProvider.CreateScope();
        return scope.ServiceProvider.GetRequiredService<IScopedService3>();
    }
}
