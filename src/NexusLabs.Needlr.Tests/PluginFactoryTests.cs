using System.Reflection;
using Xunit;

namespace NexusLabs.Needlr.Tests;

public sealed class PluginFactoryTests
{
    private readonly PluginFactory _factory = new();

    [Fact]
    public void CreatePluginsFromAssemblies_WithValidPluginInterface_ReturnsInstances()
    {
        var assemblies = new[] { typeof(PluginFactoryTests).Assembly };
        
        var plugins = _factory.CreatePluginsFromAssemblies<ITestPlugin>(assemblies);
        
        Assert.NotNull(plugins);
        var pluginList = plugins.ToList();
        Assert.Contains(pluginList, p => p is ConcreteTestPlugin);
        Assert.DoesNotContain(pluginList, p => p is AbstractTestPlugin);
    }

    [Fact]
    public void CreatePluginsFromAssemblies_WithEmptyAssemblies_ReturnsEmpty()
    {
        var assemblies = Array.Empty<Assembly>();
        
        var plugins = _factory.CreatePluginsFromAssemblies<ITestPlugin>(assemblies);
        
        Assert.NotNull(plugins);
        Assert.Empty(plugins);
    }

    [Fact]
    public void CreatePluginsFromAssemblies_WithNullAssemblies_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            _factory.CreatePluginsFromAssemblies<ITestPlugin>(null!).ToList());
    }

    [Fact]
    public void CreatePluginsFromAssemblies_WithInvalidTypes_FiltersCorrectly()
    {
        var assemblies = new[] { typeof(PluginFactoryTests).Assembly };
        
        var plugins = _factory.CreatePluginsFromAssemblies<ITestPlugin>(assemblies).ToList();
        
        // Should not include abstract classes, interfaces, or structs
        Assert.DoesNotContain(plugins, p => p.GetType() == typeof(AbstractTestPlugin));
        Assert.DoesNotContain(plugins, p => p.GetType() == typeof(ITestPlugin));
        Assert.DoesNotContain(plugins, p => p.GetType() == typeof(TestStruct));
    }

    [Fact]
    public void CreatePluginsFromAssemblies_WithGenericTypes_ExcludesGenericTypeDefinitions()
    {
        var assemblies = new[] { typeof(PluginFactoryTests).Assembly };
        
        var plugins = _factory.CreatePluginsFromAssemblies<object>(assemblies).ToList();
        
        // Should not include generic type definitions
        Assert.DoesNotContain(plugins, p => p.GetType().IsGenericTypeDefinition);
        // Should include concrete types
        Assert.Contains(plugins, p => p is ConcreteTestPlugin);
    }

    [Fact]
    public void CreatePluginsWithAttributeFromAssemblies_WithValidAttribute_ReturnsInstances()
    {
        var assemblies = new[] { typeof(PluginFactoryTests).Assembly };
        
        var plugins = _factory.CreatePluginsWithAttributeFromAssemblies<TestPluginAttribute>(assemblies);
        
        Assert.NotNull(plugins);
        var pluginList = plugins.ToList();
        Assert.Contains(pluginList, p => p is AttributedTestPlugin);
        Assert.Contains(pluginList, p => p is InheritedAttributedTestClass);
    }

    [Fact]
    public void CreatePluginsWithAttributeFromAssemblies_WithEmptyAssemblies_ReturnsEmpty()
    {
        var assemblies = Array.Empty<Assembly>();
        
        var plugins = _factory.CreatePluginsWithAttributeFromAssemblies<TestPluginAttribute>(assemblies);
        
        Assert.NotNull(plugins);
        Assert.Empty(plugins);
    }

    [Fact]
    public void CreatePluginsWithAttributeFromAssemblies_WithNullAssemblies_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            _factory.CreatePluginsWithAttributeFromAssemblies<TestPluginAttribute>(null!).ToList());
    }

    [Fact]
    public void CreatePluginsWithAttributeFromAssemblies_WithoutAttribute_ReturnsEmpty()
    {
        var assemblies = new[] { typeof(PluginFactoryTests).Assembly };
        
        var plugins = _factory.CreatePluginsWithAttributeFromAssemblies<ObsoleteAttribute>(assemblies);
        
        Assert.NotNull(plugins);
        Assert.Empty(plugins);
    }

    [Fact]
    public void CreatePluginsFromAssemblies_WithInterfaceAndAttribute_ReturnsFilteredInstances()
    {
        var assemblies = new[] { typeof(PluginFactoryTests).Assembly };
        
        var plugins = _factory.CreatePluginsFromAssemblies<ITestPlugin, TestPluginAttribute>(assemblies);
        
        Assert.NotNull(plugins);
        var pluginList = plugins.ToList();
        Assert.Contains(pluginList, p => p is AttributedTestPlugin);
        Assert.DoesNotContain(pluginList, p => p is ConcreteTestPlugin); // No attribute
        Assert.DoesNotContain(pluginList, p => p is AttributedTestClass); // No interface
    }

    [Fact]
    public void CreatePluginsFromAssemblies_WithInterfaceAndAttribute_WithEmptyAssemblies_ReturnsEmpty()
    {
        var assemblies = Array.Empty<Assembly>();
        
        var plugins = _factory.CreatePluginsFromAssemblies<ITestPlugin, TestPluginAttribute>(assemblies);
        
        Assert.NotNull(plugins);
        Assert.Empty(plugins);
    }

    [Fact]
    public void CreatePluginsFromAssemblies_WithInterfaceAndAttribute_WithNullAssemblies_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            _factory.CreatePluginsFromAssemblies<ITestPlugin, TestPluginAttribute>(null!).ToList());
    }

    [Fact]
    public void CreatePluginsFromAssemblies_WithAssemblyLoadException_HandlesGracefully()
    {
        var mockAssembly = new MockAssemblyWithLoadException();
        var assemblies = new Assembly[] { mockAssembly, typeof(PluginFactoryTests).Assembly };
        
        var plugins = _factory.CreatePluginsFromAssemblies<ITestPlugin>(assemblies);
        
        Assert.NotNull(plugins);
        var pluginList = plugins.ToList();
        // Should still get plugins from the valid assembly
        Assert.Contains(pluginList, p => p is ConcreteTestPlugin);
    }

    [Fact]
    public void CreatePluginsFromAssemblies_WithParameterlessConstructor_CreatesInstance()
    {
        var assemblies = new[] { typeof(PluginFactoryTests).Assembly };
        
        var plugins = _factory.CreatePluginsFromAssemblies<ITestPlugin>(assemblies).ToList();
        
        var concretePlugin = plugins.OfType<ConcreteTestPlugin>().FirstOrDefault();
        Assert.NotNull(concretePlugin);
        Assert.Equal("ConcreteTestPlugin", concretePlugin.Name);
    }

    [Fact]
    public void CreatePluginsFromAssemblies_WithNoParameterlessConstructor_FiltersOut()
    {
        var assemblies = new[] { typeof(PluginFactoryTests).Assembly };
        
        var plugins = _factory.CreatePluginsFromAssemblies<ITestPlugin>(assemblies).ToList();
        
        // Should not include types without parameterless constructors
        Assert.DoesNotContain(plugins, p => p is PluginWithoutParameterlessConstructor);
        // But should include valid types
        Assert.Contains(plugins, p => p is ConcreteTestPlugin);
    }

    [Fact]
    public void CreatePluginsWithAttributeFromAssemblies_WithInheritedAttribute_FindsAttribute()
    {
        var assemblies = new[] { typeof(PluginFactoryTests).Assembly };
        
        var plugins = _factory.CreatePluginsWithAttributeFromAssemblies<TestPluginAttribute>(assemblies).ToList();
        
        Assert.Contains(plugins, p => p is InheritedAttributedTestClass);
    }

    [Fact]
    public void CreatePluginsFromAssemblies_WithValueTypes_ExcludesValueTypes()
    {
        var assemblies = new[] { typeof(PluginFactoryTests).Assembly };
        
        var plugins = _factory.CreatePluginsFromAssemblies<object>(assemblies).ToList();
        
        Assert.DoesNotContain(plugins, p => p.GetType().IsValueType);
    }

    [Fact]
    public void CreatePluginsFromAssemblies_EnumeratedMultipleTimes_ReturnsSameResults()
    {
        var assemblies = new[] { typeof(PluginFactoryTests).Assembly };
        
        var plugins = _factory.CreatePluginsFromAssemblies<ITestPlugin>(assemblies);
        
        var firstEnumeration = plugins.ToList();
        var secondEnumeration = plugins.ToList();
        
        Assert.Equal(firstEnumeration.Count, secondEnumeration.Count);
        Assert.All(firstEnumeration.Zip(secondEnumeration), pair =>
        {
            Assert.Equal(pair.First.GetType(), pair.Second.GetType());
        });
    }

    [Fact]
    public void CreatePluginsFromAssemblies_WithComplexInheritanceHierarchy_WorksCorrectly()
    {
        var assemblies = new[] { typeof(PluginFactoryTests).Assembly };
        
        var plugins = _factory.CreatePluginsFromAssemblies<ITestPlugin>(assemblies).ToList();
        
        // Should include derived plugins
        Assert.Contains(plugins, p => p is DerivedTestPlugin);
        Assert.Contains(plugins, p => p is ConcreteTestPlugin);
    }

    [Fact]
    public void CreatePluginsFromAssemblies_WithMultipleAssemblies_CombinesResults()
    {
        var assembly1 = typeof(PluginFactoryTests).Assembly;
        var assembly2 = typeof(object).Assembly; // System assembly
        var assemblies = new[] { assembly1, assembly2 };
        
        var plugins = _factory.CreatePluginsFromAssemblies<ITestPlugin>(assemblies).ToList();
        
        // Should get plugins from our test assembly
        Assert.Contains(plugins, p => p is ConcreteTestPlugin);
        // System assembly shouldn't have our test interface implementations
        Assert.All(plugins, p => Assert.True(p.GetType().Assembly == assembly1));
    }
}

// Test interfaces and classes
public interface ITestPlugin
{
    string Name { get; }
}

public class ConcreteTestPlugin : ITestPlugin
{
    public string Name => "ConcreteTestPlugin";
}

public abstract class AbstractTestPlugin : ITestPlugin
{
    public abstract string Name { get; }
}

public class DerivedTestPlugin : ConcreteTestPlugin
{
    public new string Name => "DerivedTestPlugin";
}

[TestPlugin]
public class AttributedTestPlugin : ITestPlugin
{
    public string Name => "AttributedTestPlugin";
}

[TestPlugin]
public class AttributedTestClass
{
    public string Name => "AttributedTestClass";
}

public class BaseAttributedClass
{
    public string Name => "BaseAttributedClass";
}

[TestPlugin]
public class InheritedAttributedTestClass : BaseAttributedClass
{
    public new string Name => "InheritedAttributedTestClass";
}

public class PluginWithoutParameterlessConstructor : ITestPlugin
{
    public PluginWithoutParameterlessConstructor(string name)
    {
        Name = name;
    }
    
    public string Name { get; }
}

public class TestPluginAttribute : Attribute
{
}

public struct TestStruct
{
    public string Name { get; set; }
}

public class GenericTestPlugin<T> : ITestPlugin
{
    public string Name => "GenericTestPlugin";
}

// Mock assembly for testing ReflectionTypeLoadException handling
public class MockAssemblyWithLoadException : Assembly
{
    public override Type[] GetTypes()
    {
        var types = new Type?[] { typeof(ConcreteTestPlugin), null };
        var exceptions = new Exception[] { new TypeLoadException("Mock exception"), new TypeLoadException("Another mock exception") };
        throw new ReflectionTypeLoadException(types, exceptions);
    }

    public override string? FullName => "MockAssembly";
}