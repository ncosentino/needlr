using BenchmarkDotNet.Attributes;

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

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
/// Manual DI is the baseline.
/// </summary>
[Config(typeof(BenchmarkConfig))]
public class WebApplicationBuildBenchmarks
{
    private Assembly[] _assemblies = null!;
    private WebApplication? _lastApp;

    [GlobalSetup]
    public void Setup()
    {
        _assemblies = [typeof(SimpleService1).Assembly];
    }

    [IterationCleanup]
    public void IterationCleanup()
    {
        if (_lastApp != null)
        {
            _lastApp.DisposeAsync().AsTask().GetAwaiter().GetResult();
            _lastApp = null;
        }
    }

    [Benchmark(Baseline = true)]
    public WebApplication ManualDI_BuildWebApp()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddSingleton<ISimpleService1, SimpleService1>();
        builder.Services.AddSingleton<ISimpleService2, SimpleService2>();
        builder.Services.AddSingleton<ISimpleService3, SimpleService3>();
        _lastApp = builder.Build();
        return _lastApp;
    }

    [Benchmark]
    public WebApplication Needlr_Reflection_BuildWebApp()
    {
        _lastApp = new Syringe()
            .UsingReflection()
            .UsingAdditionalAssemblies(_assemblies)
            .BuildWebApplication();
        return _lastApp;
    }

    [Benchmark]
    public WebApplication Needlr_SourceGen_BuildWebApp()
    {
        _lastApp = new Syringe()
            .UsingSourceGen()
            .UsingAdditionalAssemblies(_assemblies)
            .BuildWebApplication();
        return _lastApp;
    }

    [Benchmark]
    public WebApplication Needlr_SourceGenExplicit_BuildWebApp()
    {
        _lastApp = new Syringe()
            .UsingGeneratedComponents(
                NexusLabs.Needlr.Benchmarks.Generated.TypeRegistry.GetInjectableTypes,
                NexusLabs.Needlr.Benchmarks.Generated.TypeRegistry.GetPluginTypes)
            .UsingAdditionalAssemblies(_assemblies)
            .BuildWebApplication();
        return _lastApp;
    }
}
