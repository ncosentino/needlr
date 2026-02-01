using BenchmarkDotNet.Attributes;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using NexusLabs.Needlr.Benchmarks.TestTypes;
using NexusLabs.Needlr.Injection;
using NexusLabs.Needlr.Injection.Reflection;
using NexusLabs.Needlr.Injection.SourceGen;

using System.Reflection;

namespace NexusLabs.Needlr.Benchmarks.Benchmarks;

/// <summary>
/// Benchmarks comparing IOptions resolution between source-gen and reflection.
/// All benchmarks measure resolution of a single options type.
/// Reflection is the baseline.
/// </summary>
[Config(typeof(BenchmarkConfig))]
public class OptionsResolutionBenchmarks
{
    private IServiceProvider _reflectionProvider = null!;
    private IServiceProvider _sourceGenProvider = null!;
    private IConfiguration _configuration = null!;
    private Assembly[] _assemblies = null!;

    [GlobalSetup]
    public void Setup()
    {
        var configData = new Dictionary<string, string?>
        {
            ["Benchmark:Cache:TimeoutSeconds"] = "600",
            ["Benchmark:Cache:MaxItems"] = "2000",
            ["Benchmark:Cache:EnableLogging"] = "true"
        };

        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

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
    public CacheOptions ResolveOptions_Reflection()
    {
        return _reflectionProvider.GetRequiredService<IOptions<CacheOptions>>().Value;
    }

    [Benchmark]
    public CacheOptions ResolveOptions_SourceGen()
    {
        return _sourceGenProvider.GetRequiredService<IOptions<CacheOptions>>().Value;
    }
}
