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
/// Benchmarks comparing resolution of services with deep dependency chains.
/// Tests a 10-level deep dependency graph where each level depends on the previous.
/// Real applications often have complex dependency graphs; this measures scaling behavior.
/// Manual DI is the baseline.
/// </summary>
[Config(typeof(BenchmarkConfig))]
public class DeepDependencyChainBenchmarks
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
        _assemblies = [typeof(DeepLevel1).Assembly];

        // Manual DI registration of 10-level deep chain
        var manualServices = new ServiceCollection();
        manualServices.AddSingleton<IDeepLevel1, DeepLevel1>();
        manualServices.AddSingleton<IDeepLevel2, DeepLevel2>();
        manualServices.AddSingleton<IDeepLevel3, DeepLevel3>();
        manualServices.AddSingleton<IDeepLevel4, DeepLevel4>();
        manualServices.AddSingleton<IDeepLevel5, DeepLevel5>();
        manualServices.AddSingleton<IDeepLevel6, DeepLevel6>();
        manualServices.AddSingleton<IDeepLevel7, DeepLevel7>();
        manualServices.AddSingleton<IDeepLevel8, DeepLevel8>();
        manualServices.AddSingleton<IDeepLevel9, DeepLevel9>();
        manualServices.AddSingleton<IDeepLevel10, DeepLevel10>();
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
    public IDeepLevel10 ManualDI_ResolveDeepChain()
    {
        return _manualProvider.GetRequiredService<IDeepLevel10>();
    }

    [Benchmark]
    public IDeepLevel10 Needlr_Reflection_ResolveDeepChain()
    {
        return _reflectionProvider.GetRequiredService<IDeepLevel10>();
    }

    [Benchmark]
    public IDeepLevel10 Needlr_SourceGen_ResolveDeepChain()
    {
        return _sourceGenProvider.GetRequiredService<IDeepLevel10>();
    }
}
