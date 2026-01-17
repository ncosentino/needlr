using System.Reflection;

using NexusLabs.Needlr.Generators;
using NexusLabs.Needlr.Injection.PluginFactories;

using Xunit;

namespace NexusLabs.Needlr.Injection.Tests;

/// <summary>
/// Parity tests that verify the GeneratedPluginFactory produces the same
/// results as the reflection-based PluginFactory when configured with the same types.
/// </summary>
public sealed class PluginFactoryParityTests
{
    // Test plugin interfaces and implementations
    public interface ITestPlugin { }
    public interface IAnotherPlugin { }

    public class SimplePlugin : ITestPlugin { }
    public class MultiInterfacePlugin : ITestPlugin, IAnotherPlugin { }
    public class PluginWithParameterlessConstructor : ITestPlugin { }
    public class AnotherValidPlugin : ITestPlugin { }

    [Fact]
    public void CreatePluginsFromAssemblies_ReturnsSamePluginTypes()
    {
        // Arrange - configure generated factory with the same types reflection would find
        var assemblies = new[] { Assembly.GetExecutingAssembly() };
        var reflectionFactory = new PluginFactory();
        var generatedFactory = CreateGeneratedPluginFactoryMatchingReflection();

        // Act
        var reflectionPlugins = reflectionFactory
            .CreatePluginsFromAssemblies<ITestPlugin>(assemblies)
            .Select(p => p.GetType())
            .OrderBy(t => t.FullName)
            .ToList();

        var generatedPlugins = generatedFactory
            .CreatePluginsFromAssemblies<ITestPlugin>(assemblies)
            .Select(p => p.GetType())
            .OrderBy(t => t.FullName)
            .ToList();

        // Assert - both should find the same types
        Assert.Equal(reflectionPlugins.Count, generatedPlugins.Count);
        for (int i = 0; i < reflectionPlugins.Count; i++)
        {
            Assert.Equal(reflectionPlugins[i], generatedPlugins[i]);
        }
    }

    [Fact]
    public void CreatePluginsFromAssemblies_BothInstantiatePlugins()
    {
        // Arrange
        var assemblies = new[] { Assembly.GetExecutingAssembly() };
        var reflectionFactory = new PluginFactory();
        var generatedFactory = CreateGeneratedPluginFactoryMatchingReflection();

        // Act
        var reflectionPlugins = reflectionFactory
            .CreatePluginsFromAssemblies<ITestPlugin>(assemblies)
            .ToList();

        var generatedPlugins = generatedFactory
            .CreatePluginsFromAssemblies<ITestPlugin>(assemblies)
            .ToList();

        // Assert - both should return actual instances
        Assert.All(reflectionPlugins, p => Assert.NotNull(p));
        Assert.All(generatedPlugins, p => Assert.NotNull(p));
    }

    [Fact]
    public void CreatePluginsFromAssemblies_BothFindMultiInterfacePlugins()
    {
        // Arrange
        var assemblies = new[] { Assembly.GetExecutingAssembly() };
        var reflectionFactory = new PluginFactory();
        var generatedFactory = CreateGeneratedPluginFactoryMatchingReflection();

        // Act
        var reflectionPlugins = reflectionFactory
            .CreatePluginsFromAssemblies<IAnotherPlugin>(assemblies)
            .Select(p => p.GetType())
            .ToList();

        var generatedPlugins = generatedFactory
            .CreatePluginsFromAssemblies<IAnotherPlugin>(assemblies)
            .Select(p => p.GetType())
            .ToList();

        // Assert - both should find MultiInterfacePlugin
        Assert.Contains(typeof(MultiInterfacePlugin), reflectionPlugins);
        Assert.Contains(typeof(MultiInterfacePlugin), generatedPlugins);
    }

    [Fact]
    public void CreatePluginsFromAssemblies_EmptyAssemblies_BothReturnEmpty()
    {
        // Arrange
        var assemblies = Array.Empty<Assembly>();
        var reflectionFactory = new PluginFactory();
        var generatedFactory = CreateGeneratedPluginFactoryMatchingReflection();

        // Act
        var reflectionPlugins = reflectionFactory
            .CreatePluginsFromAssemblies<ITestPlugin>(assemblies)
            .ToList();

        var generatedPlugins = generatedFactory
            .CreatePluginsFromAssemblies<ITestPlugin>(assemblies)
            .ToList();

        // Assert
        Assert.Empty(reflectionPlugins);
        Assert.Empty(generatedPlugins);
    }

    [Fact]
    public void CreatePluginsFromAssemblies_UnmatchedAssembly_BothReturnEmpty()
    {
        // Arrange - use a system assembly that won't have our plugins
        var assemblies = new[] { typeof(object).Assembly };
        var reflectionFactory = new PluginFactory();
        var generatedFactory = CreateGeneratedPluginFactoryMatchingReflection();

        // Act
        var reflectionPlugins = reflectionFactory
            .CreatePluginsFromAssemblies<ITestPlugin>(assemblies)
            .ToList();

        var generatedPlugins = generatedFactory
            .CreatePluginsFromAssemblies<ITestPlugin>(assemblies)
            .ToList();

        // Assert
        Assert.Empty(reflectionPlugins);
        Assert.Empty(generatedPlugins);
    }

    [Fact]
    public void GeneratedPluginFactory_UsesFactoryDelegateNotActivator()
    {
        // Arrange - create a factory with custom plugin provider that tracks calls
        var factoryCalled = false;
        var plugins = new List<PluginTypeInfo>
        {
            new(
                typeof(SimplePlugin),
                new[] { typeof(ITestPlugin) },
                () =>
                {
                    factoryCalled = true;
                    return new SimplePlugin();
                })
        };

        var factory = new GeneratedPluginFactory(() => plugins);

        // Act
        var result = factory
            .CreatePluginsFromAssemblies<ITestPlugin>(new[] { Assembly.GetExecutingAssembly() })
            .ToList();

        // Assert
        Assert.True(factoryCalled, "Factory delegate should have been called instead of Activator.CreateInstance");
        Assert.Single(result);
        Assert.IsType<SimplePlugin>(result[0]);
    }

    [Fact]
    public void GeneratedPluginFactory_FiltersPluginsByInterface()
    {
        // Arrange
        var plugins = new List<PluginTypeInfo>
        {
            new(typeof(SimplePlugin), new[] { typeof(ITestPlugin) }, () => new SimplePlugin()),
            new(typeof(MultiInterfacePlugin), new[] { typeof(ITestPlugin), typeof(IAnotherPlugin) }, () => new MultiInterfacePlugin())
        };

        var factory = new GeneratedPluginFactory(() => plugins);
        var assemblies = new[] { Assembly.GetExecutingAssembly() };

        // Act - only request IAnotherPlugin
        var result = factory
            .CreatePluginsFromAssemblies<IAnotherPlugin>(assemblies)
            .ToList();

        // Assert - only MultiInterfacePlugin implements IAnotherPlugin
        Assert.Single(result);
        Assert.IsType<MultiInterfacePlugin>(result[0]);
    }

    /// <summary>
    /// Creates a GeneratedPluginFactory configured with the same types
    /// that reflection would discover in this test class.
    /// </summary>
    private static GeneratedPluginFactory CreateGeneratedPluginFactoryMatchingReflection()
    {
        // Include all plugins that reflection would find:
        // SimplePlugin, MultiInterfacePlugin, PluginWithParameterlessConstructor, AnotherValidPlugin
        var plugins = new List<PluginTypeInfo>
        {
            new(typeof(AnotherValidPlugin), new[] { typeof(ITestPlugin) }, () => new AnotherValidPlugin()),
            new(typeof(MultiInterfacePlugin), new[] { typeof(ITestPlugin), typeof(IAnotherPlugin) }, () => new MultiInterfacePlugin()),
            new(typeof(PluginWithParameterlessConstructor), new[] { typeof(ITestPlugin) }, () => new PluginWithParameterlessConstructor()),
            new(typeof(SimplePlugin), new[] { typeof(ITestPlugin) }, () => new SimplePlugin())
        };

        return new GeneratedPluginFactory(() => plugins);
    }
}
