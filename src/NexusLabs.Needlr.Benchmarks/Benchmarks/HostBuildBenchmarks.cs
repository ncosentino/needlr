using BenchmarkDotNet.Attributes;

using Microsoft.Extensions.Hosting;

using NexusLabs.Needlr.Benchmarks.TestTypes;
using NexusLabs.Needlr.Hosting;
using NexusLabs.Needlr.Injection;
using NexusLabs.Needlr.Injection.Reflection;
using NexusLabs.Needlr.Injection.SourceGen;

using System.Reflection;

namespace NexusLabs.Needlr.Benchmarks.Benchmarks;

/// <summary>
/// Benchmarks comparing host build with source-gen vs reflection DI.
/// Measures full host building time including service registration.
/// Reflection is the baseline.
/// </summary>
[Config(typeof(BenchmarkConfig))]
public class HostBuildBenchmarks
{
    private Assembly[] _assemblies = null!;
    private IHost? _lastHost;

    [GlobalSetup]
    public void Setup()
    {
        _assemblies = [typeof(SimpleService1).Assembly];
    }

    [IterationCleanup]
    public void IterationCleanup()
    {
        _lastHost?.Dispose();
        _lastHost = null;
    }

    [Benchmark(Baseline = true)]
    public IHost BuildHost_Reflection()
    {
        _lastHost = new Syringe()
            .UsingReflection()
            .UsingAdditionalAssemblies(_assemblies)
            .BuildHost();
        return _lastHost;
    }

    [Benchmark]
    public IHost BuildHost_SourceGen()
    {
        _lastHost = new Syringe()
            .UsingSourceGen()
            .UsingAdditionalAssemblies(_assemblies)
            .BuildHost();
        return _lastHost;
    }

    [Benchmark]
    public IHost BuildHost_SourceGenExplicit()
    {
        _lastHost = new Syringe()
            .UsingGeneratedComponents(
                NexusLabs.Needlr.Benchmarks.Generated.TypeRegistry.GetInjectableTypes,
                NexusLabs.Needlr.Benchmarks.Generated.TypeRegistry.GetPluginTypes)
            .UsingAdditionalAssemblies(_assemblies)
            .BuildHost();
        return _lastHost;
    }
}
