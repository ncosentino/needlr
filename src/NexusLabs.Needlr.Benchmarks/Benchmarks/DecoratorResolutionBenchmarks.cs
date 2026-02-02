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
/// Benchmarks comparing decorated service resolution between source-gen and reflection.
/// All benchmarks measure resolution of a service with a 5-decorator chain.
/// Manual DI is the baseline.
/// </summary>
[Config(typeof(BenchmarkConfig))]
public class DecoratorResolutionBenchmarks
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

        // Manual DI registration with decorator chain
        var manualServices = new ServiceCollection();
        manualServices.AddSingleton<IDecoratedService>(sp =>
        {
            IDecoratedService inner = new DecoratedServiceImpl();
            inner = new Decorator1(inner);
            inner = new Decorator2(inner);
            inner = new Decorator3(inner);
            inner = new Decorator4(inner);
            inner = new Decorator5(inner);
            return inner;
        });
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
    public IDecoratedService ManualDI_ResolveDecorated()
    {
        return _manualProvider.GetRequiredService<IDecoratedService>();
    }

    [Benchmark]
    public IDecoratedService Needlr_Reflection_ResolveDecorated()
    {
        return _reflectionProvider.GetRequiredService<IDecoratedService>();
    }

    [Benchmark]
    public IDecoratedService Needlr_SourceGen_ResolveDecorated()
    {
        return _sourceGenProvider.GetRequiredService<IDecoratedService>();
    }
}
