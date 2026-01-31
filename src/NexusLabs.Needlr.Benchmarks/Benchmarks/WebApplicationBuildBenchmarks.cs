using BenchmarkDotNet.Attributes;

using Microsoft.AspNetCore.Builder;

using NexusLabs.Needlr.AspNet;
using NexusLabs.Needlr.Benchmarks.TestTypes;
using NexusLabs.Needlr.Injection;
using NexusLabs.Needlr.Injection.Reflection;
using NexusLabs.Needlr.Injection.SourceGen;

using System.Reflection;

namespace NexusLabs.Needlr.Benchmarks.Benchmarks;

/// <summary>
/// Benchmarks comparing ASP.NET WebApplication build with source-gen vs reflection DI.
/// Measures full WebApplication building time including service registration.
/// Reflection is the baseline.
/// </summary>
[Config(typeof(BenchmarkConfig))]
public class WebApplicationBuildBenchmarks
{
    private Assembly[] _assemblies = null!;

    [GlobalSetup]
    public void Setup()
    {
        _assemblies = [typeof(SimpleService1).Assembly];
    }

    [Benchmark(Baseline = true)]
    public WebApplication Reflection()
    {
        return new Syringe()
            .UsingReflection()
            .UsingAdditionalAssemblies(_assemblies)
            .BuildWebApplication();
    }

    [Benchmark]
    public WebApplication SourceGen()
    {
        return new Syringe()
            .UsingSourceGen()
            .UsingAdditionalAssemblies(_assemblies)
            .BuildWebApplication();
    }

    [Benchmark]
    public WebApplication SourceGenExplicit()
    {
        return new Syringe()
            .UsingGeneratedComponents(
                NexusLabs.Needlr.Benchmarks.Generated.TypeRegistry.GetInjectableTypes,
                NexusLabs.Needlr.Benchmarks.Generated.TypeRegistry.GetPluginTypes)
            .UsingAdditionalAssemblies(_assemblies)
            .BuildWebApplication();
    }
}
