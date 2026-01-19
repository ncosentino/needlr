using BenchmarkDotNet.Attributes;

using NexusLabs.Needlr.Benchmarks.TestTypes;
using NexusLabs.Needlr.Injection.Reflection.PluginFactories;
using NexusLabs.Needlr.Injection.SourceGen.PluginFactories;

using System.Reflection;

namespace NexusLabs.Needlr.Benchmarks.Benchmarks;

/// <summary>
/// Benchmarks comparing GeneratedPluginFactory vs ReflectionPluginFactory
/// for IServiceCollectionPlugin discovery.
/// Uses REAL generated TypeRegistry from source generator.
/// Reflection is the baseline.
/// </summary>
[Config(typeof(BenchmarkConfig))]
public class ServiceCollectionPluginBenchmarks
{
    private Assembly[] _assemblies = null!;
    private ReflectionPluginFactory _reflectionFactory = null!;
    private GeneratedPluginFactory _sourceGenFactory = null!;

    [GlobalSetup]
    public void Setup()
    {
        _assemblies = [typeof(ServiceCollectionPlugin1).Assembly];
        _reflectionFactory = new ReflectionPluginFactory();
        _sourceGenFactory = new GeneratedPluginFactory(
            NexusLabs.Needlr.Generated.TypeRegistry.GetPluginTypes,
            allowAllWhenAssembliesEmpty: false);
    }

    [Benchmark(Baseline = true)]
    public List<IServiceCollectionPlugin> Reflection()
    {
        return _reflectionFactory.CreatePluginsFromAssemblies<IServiceCollectionPlugin>(_assemblies).ToList();
    }

    [Benchmark]
    public List<IServiceCollectionPlugin> SourceGen()
    {
        return _sourceGenFactory.CreatePluginsFromAssemblies<IServiceCollectionPlugin>(_assemblies).ToList();
    }
}

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

/// <summary>
/// Benchmarks comparing GeneratedPluginFactory vs ReflectionPluginFactory
/// for discovering ALL plugin types (both IServiceCollectionPlugin and IPostBuildServiceCollectionPlugin).
/// Uses REAL generated TypeRegistry from source generator.
/// Reflection is the baseline.
/// </summary>
[Config(typeof(BenchmarkConfig))]
public class AllPluginsBenchmarks
{
    private Assembly[] _assemblies = null!;
    private ReflectionPluginFactory _reflectionFactory = null!;
    private GeneratedPluginFactory _sourceGenFactory = null!;

    [GlobalSetup]
    public void Setup()
    {
        _assemblies = [typeof(ServiceCollectionPlugin1).Assembly];
        _reflectionFactory = new ReflectionPluginFactory();
        _sourceGenFactory = new GeneratedPluginFactory(
            NexusLabs.Needlr.Generated.TypeRegistry.GetPluginTypes,
            allowAllWhenAssembliesEmpty: false);
    }

    [Benchmark(Baseline = true)]
    public int Reflection()
    {
        var servicePlugins = _reflectionFactory.CreatePluginsFromAssemblies<IServiceCollectionPlugin>(_assemblies).Count();
        var postBuildPlugins = _reflectionFactory.CreatePluginsFromAssemblies<IPostBuildServiceCollectionPlugin>(_assemblies).Count();
        return servicePlugins + postBuildPlugins;
    }

    [Benchmark]
    public int SourceGen()
    {
        var servicePlugins = _sourceGenFactory.CreatePluginsFromAssemblies<IServiceCollectionPlugin>(_assemblies).Count();
        var postBuildPlugins = _sourceGenFactory.CreatePluginsFromAssemblies<IPostBuildServiceCollectionPlugin>(_assemblies).Count();
        return servicePlugins + postBuildPlugins;
    }
}
