using BenchmarkDotNet.Attributes;

using NexusLabs.Needlr.Benchmarks.TestTypes;
using NexusLabs.Needlr.Injection.Reflection.PluginFactories;
using NexusLabs.Needlr.Injection.SourceGen.PluginFactories;

using System.Reflection;

namespace NexusLabs.Needlr.Benchmarks.Benchmarks;

/// <summary>
/// Benchmarks comparing GeneratedPluginFactory vs ReflectionPluginFactory
/// for IPostBuildServiceCollectionPlugin discovery.
/// Uses REAL generated TypeRegistry from source generator.
/// Reflection is the baseline.
/// </summary>
[Config(typeof(BenchmarkConfig))]
public class PostBuildPluginBenchmarks
{
    private Assembly[] _assemblies = null!;
    private ReflectionPluginFactory _reflectionFactory = null!;
    private GeneratedPluginFactory _sourceGenFactory = null!;

    [GlobalSetup]
    public void Setup()
    {
        _assemblies = [typeof(PostBuildPlugin1).Assembly];
        _reflectionFactory = new ReflectionPluginFactory();
        _sourceGenFactory = new GeneratedPluginFactory(
            NexusLabs.Needlr.Generated.TypeRegistry.GetPluginTypes,
            allowAllWhenAssembliesEmpty: false);
    }

    [Benchmark(Baseline = true)]
    public List<IPostBuildServiceCollectionPlugin> Reflection()
    {
        return _reflectionFactory.CreatePluginsFromAssemblies<IPostBuildServiceCollectionPlugin>(_assemblies).ToList();
    }

    [Benchmark]
    public List<IPostBuildServiceCollectionPlugin> SourceGen()
    {
        return _sourceGenFactory.CreatePluginsFromAssemblies<IPostBuildServiceCollectionPlugin>(_assemblies).ToList();
    }
}
