using System.Reflection;

using NexusLabs.Needlr.AspNet;
using NexusLabs.Needlr.Generators;
using NexusLabs.Needlr.Injection.Reflection.PluginFactories;
using NexusLabs.Needlr.Injection.SourceGen.PluginFactories;

using Xunit;

namespace NexusLabs.Needlr.Carter.Tests;

/// <summary>
/// Integration tests verifying Carter plugin discovery works with both
/// reflection and source generation paths.
/// </summary>
public sealed class SourceGenIntegrationTests
{
    [Fact]
    public void Carter_PackageHasOwnTypeRegistry()
    {
        var carterAssembly = typeof(CarterWebApplicationBuilderPlugin).Assembly;
        var typeRegistryType = carterAssembly.GetType("NexusLabs.Needlr.Carter.Generated.TypeRegistry");

        Assert.NotNull(typeRegistryType);
        var getPluginTypesMethod = typeRegistryType.GetMethod("GetPluginTypes");
        Assert.NotNull(getPluginTypesMethod);
    }

    [Fact]
    public void Carter_PackageHasModuleInitializer()
    {
        var carterAssembly = typeof(CarterWebApplicationBuilderPlugin).Assembly;
        var moduleInitializerType = carterAssembly.GetType("NexusLabs.Needlr.Carter.Generated.NeedlrSourceGenModuleInitializer");

        Assert.NotNull(moduleInitializerType);
    }

    [Fact]
    public void Carter_PluginsRegisteredViaOwnTypeRegistry()
    {
        var carterAssembly = typeof(CarterWebApplicationBuilderPlugin).Assembly;
        var typeRegistryType = carterAssembly.GetType("NexusLabs.Needlr.Carter.Generated.TypeRegistry");
        Assert.NotNull(typeRegistryType);

        var getPluginTypesMethod = typeRegistryType.GetMethod("GetPluginTypes");
        Assert.NotNull(getPluginTypesMethod);

        var pluginTypes = (IReadOnlyList<PluginTypeInfo>)getPluginTypesMethod.Invoke(null, null)!;
        var pluginTypeNames = pluginTypes.Select(p => p.PluginType.Name).ToList();

        Assert.Contains(pluginTypeNames, n => n == "CarterWebApplicationBuilderPlugin");
        Assert.Contains(pluginTypeNames, n => n == "CarterWebApplicationPlugin");
    }

    [Fact]
    public void Carter_ModuleInitializerRegistersPlugins()
    {
        if (!NeedlrSourceGenBootstrap.TryGetProviders(out _, out var pluginProvider))
        {
            Assert.Fail("NeedlrSourceGenBootstrap has no registered providers");
            return;
        }

        var allPluginTypes = pluginProvider().ToList();
        var carterPluginTypes = allPluginTypes
            .Where(p => p.PluginType.Assembly == typeof(CarterWebApplicationBuilderPlugin).Assembly)
            .Select(p => p.PluginType.Name)
            .ToList();

        Assert.Contains(carterPluginTypes, n => n == "CarterWebApplicationBuilderPlugin");
        Assert.Contains(carterPluginTypes, n => n == "CarterWebApplicationPlugin");
    }

    [Fact]
    public void PluginParity_IWebApplicationBuilderPlugin_BothFactoriesDiscoverSamePlugins()
    {
        var carterAssembly = typeof(CarterWebApplicationBuilderPlugin).Assembly;
        var assemblies = new[] { carterAssembly };
        var reflectionFactory = new ReflectionPluginFactory();
        var generatedFactory = new GeneratedPluginFactory(
            Carter.Generated.TypeRegistry.GetPluginTypes);

        var reflectionPlugins = reflectionFactory
            .CreatePluginsFromAssemblies<IWebApplicationBuilderPlugin>(assemblies)
            .Select(p => p.GetType().FullName)
            .OrderBy(n => n)
            .ToList();

        var generatedPlugins = generatedFactory
            .CreatePluginsFromAssemblies<IWebApplicationBuilderPlugin>(assemblies)
            .Select(p => p.GetType().FullName)
            .OrderBy(n => n)
            .ToList();

        Assert.Equal(reflectionPlugins, generatedPlugins);
        Assert.Contains(reflectionPlugins, n => n == "NexusLabs.Needlr.Carter.CarterWebApplicationBuilderPlugin");
    }

    [Fact]
    public void PluginParity_IWebApplicationPlugin_BothFactoriesDiscoverSamePlugins()
    {
        var carterAssembly = typeof(CarterWebApplicationPlugin).Assembly;
        var assemblies = new[] { carterAssembly };
        var reflectionFactory = new ReflectionPluginFactory();
        var generatedFactory = new GeneratedPluginFactory(
            Carter.Generated.TypeRegistry.GetPluginTypes);

        var reflectionPlugins = reflectionFactory
            .CreatePluginsFromAssemblies<IWebApplicationPlugin>(assemblies)
            .Select(p => p.GetType().FullName)
            .OrderBy(n => n)
            .ToList();

        var generatedPlugins = generatedFactory
            .CreatePluginsFromAssemblies<IWebApplicationPlugin>(assemblies)
            .Select(p => p.GetType().FullName)
            .OrderBy(n => n)
            .ToList();

        Assert.Equal(reflectionPlugins, generatedPlugins);
        Assert.Contains(reflectionPlugins, n => n == "NexusLabs.Needlr.Carter.CarterWebApplicationPlugin");
    }

    [Fact]
    public void PluginParity_AllPluginTypes_IdenticalBetweenReflectionAndGenerated()
    {
        var carterAssembly = typeof(CarterWebApplicationBuilderPlugin).Assembly;
        var assemblies = new[] { carterAssembly };
        var reflectionFactory = new ReflectionPluginFactory();
        var generatedFactory = new GeneratedPluginFactory(
            Carter.Generated.TypeRegistry.GetPluginTypes);

        var reflectionBuilderPlugins = reflectionFactory
            .CreatePluginsFromAssemblies<IWebApplicationBuilderPlugin>(assemblies)
            .Select(p => p.GetType().FullName)
            .ToList();
        var reflectionAppPlugins = reflectionFactory
            .CreatePluginsFromAssemblies<IWebApplicationPlugin>(assemblies)
            .Select(p => p.GetType().FullName)
            .ToList();

        var generatedBuilderPlugins = generatedFactory
            .CreatePluginsFromAssemblies<IWebApplicationBuilderPlugin>(assemblies)
            .Select(p => p.GetType().FullName)
            .ToList();
        var generatedAppPlugins = generatedFactory
            .CreatePluginsFromAssemblies<IWebApplicationPlugin>(assemblies)
            .Select(p => p.GetType().FullName)
            .ToList();

        Assert.Single(reflectionBuilderPlugins);
        Assert.Single(generatedBuilderPlugins);
        Assert.Single(reflectionAppPlugins);
        Assert.Single(generatedAppPlugins);

        Assert.Equal(reflectionBuilderPlugins.OrderBy(x => x), generatedBuilderPlugins.OrderBy(x => x));
        Assert.Equal(reflectionAppPlugins.OrderBy(x => x), generatedAppPlugins.OrderBy(x => x));
    }
}
