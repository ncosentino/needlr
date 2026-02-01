using BenchmarkDotNet.Attributes;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using NexusLabs.Needlr.Benchmarks.TestTypes;
using NexusLabs.Needlr.Injection;
using NexusLabs.Needlr.Injection.Reflection;
using NexusLabs.Needlr.Injection.SourceGen;

namespace NexusLabs.Needlr.Benchmarks.Benchmarks;

/// <summary>
/// Benchmarks comparing service resolution approaches.
/// Measures the time to resolve a type from a pre-built provider.
/// <list type="bullet">
///   <item>Baseline: Manual registration + ServiceProvider.GetService</item>
///   <item>Needlr Reflection: Syringe reflection + ServiceProvider.GetService</item>
///   <item>Needlr Source Gen: Syringe source gen + ServiceProvider.GetService</item>
///   <item>Provider Interface: Source gen provider property access</item>
///   <item>Provider Shorthand: Source gen shorthand provider property access</item>
/// </list>
/// </summary>
[Config(typeof(BenchmarkConfig))]
public class ProviderResolutionBenchmarks
{
    private IServiceProvider _baselineProvider = null!;
    private IServiceProvider _reflectionProvider = null!;
    private IServiceProvider _sourceGenProvider = null!;
    private ISingleServiceProvider _interfaceProvider = null!;
    private ISingleShorthandProvider _shorthandProvider = null!;
    private IConfiguration _configuration = null!;

    [GlobalSetup]
    public void Setup()
    {
        _configuration = new ConfigurationBuilder().Build();

        // Baseline: manual registration
        var baselineServices = new ServiceCollection();
        baselineServices.AddSingleton<IProviderDependency1, ProviderDependency1>();
        baselineServices.AddSingleton<IProviderDependency2, ProviderDependency2>();
        baselineServices.AddSingleton<IProviderDependency3, ProviderDependency3>();
        baselineServices.AddSingleton<IProviderTargetService, ProviderTargetService>();
        _baselineProvider = baselineServices.BuildServiceProvider();

        // Needlr reflection
        _reflectionProvider = new Syringe()
            .UsingReflection()
            .BuildServiceProvider(_configuration);

        // Needlr source gen
        _sourceGenProvider = new Syringe()
            .UsingSourceGen()
            .BuildServiceProvider(_configuration);

        // Providers are singletons registered by source gen
        _interfaceProvider = _sourceGenProvider.GetRequiredService<ISingleServiceProvider>();
        _shorthandProvider = _sourceGenProvider.GetRequiredService<ISingleShorthandProvider>();
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        (_baselineProvider as IDisposable)?.Dispose();
        (_reflectionProvider as IDisposable)?.Dispose();
        (_sourceGenProvider as IDisposable)?.Dispose();
    }

    [Benchmark(Baseline = true)]
    public IProviderTargetService ManualDI_ServiceProvider_GetService()
    {
        return _baselineProvider.GetRequiredService<IProviderTargetService>();
    }

    [Benchmark]
    public IProviderTargetService Needlr_Reflection_GetService()
    {
        return _reflectionProvider.GetRequiredService<IProviderTargetService>();
    }

    [Benchmark]
    public IProviderTargetService Needlr_SourceGen_GetService()
    {
        return _sourceGenProvider.GetRequiredService<IProviderTargetService>();
    }

    [Benchmark]
    public IProviderTargetService Provider_Interface_PropertyAccess()
    {
        return _interfaceProvider.TargetService;
    }

    [Benchmark]
    public IProviderTargetService Provider_Shorthand_PropertyAccess()
    {
        return _shorthandProvider.ProviderTargetService;
    }
}
