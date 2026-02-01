using BenchmarkDotNet.Attributes;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using NexusLabs.Needlr.Benchmarks.TestTypes;
using NexusLabs.Needlr.Injection;
using NexusLabs.Needlr.Injection.Reflection;
using NexusLabs.Needlr.Injection.SourceGen;

namespace NexusLabs.Needlr.Benchmarks.Benchmarks;

/// <summary>
/// Benchmarks measuring the complete path from scratch to a type that can resolve services.
/// <list type="bullet">
///   <item>Baseline: Manual registration → IServiceProvider</item>
///   <item>Needlr Reflection: Syringe reflection → IServiceProvider</item>
///   <item>Needlr Source Gen: Syringe source gen → IServiceProvider</item>
///   <item>Needlr Provider: Syringe source gen + resolve provider → ISingleServiceProvider</item>
/// </list>
/// </summary>
[Config(typeof(BenchmarkConfig))]
public class ProviderBuildBenchmarks
{
    private IConfiguration _configuration = null!;

    [GlobalSetup]
    public void Setup()
    {
        _configuration = new ConfigurationBuilder().Build();
    }

    [Benchmark(Baseline = true)]
    public IServiceProvider ManualDI_ToServiceProvider()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IProviderDependency1, ProviderDependency1>();
        services.AddSingleton<IProviderDependency2, ProviderDependency2>();
        services.AddSingleton<IProviderDependency3, ProviderDependency3>();
        services.AddSingleton<IProviderTargetService, ProviderTargetService>();
        return services.BuildServiceProvider();
    }

    [Benchmark]
    public IServiceProvider Needlr_Reflection_ToServiceProvider()
    {
        return new Syringe()
            .UsingReflection()
            .BuildServiceProvider(_configuration);
    }

    [Benchmark]
    public IServiceProvider Needlr_SourceGen_ToServiceProvider()
    {
        return new Syringe()
            .UsingSourceGen()
            .BuildServiceProvider(_configuration);
    }

    [Benchmark]
    public ISingleServiceProvider Needlr_SourceGen_ToProvider()
    {
        var sp = new Syringe()
            .UsingSourceGen()
            .BuildServiceProvider(_configuration);
        return sp.GetRequiredService<ISingleServiceProvider>();
    }
}
