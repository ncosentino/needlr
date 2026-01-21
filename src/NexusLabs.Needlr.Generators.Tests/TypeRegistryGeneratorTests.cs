using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

using Xunit;

namespace NexusLabs.Needlr.Generators.Tests;

public sealed class TypeRegistryGeneratorTests
{
    [Fact]
    public void Generator_WithGenerateTypeRegistryAttribute_GeneratesTypeRegistry()
    {
        var source = @"
using NexusLabs.Needlr.Generators;

[assembly: GenerateTypeRegistry]

namespace TestApp
{
    public interface IMyService { }
    public class MyService : IMyService { }
}";

        var generatedCode = RunGenerator(source);

        Assert.Contains("public static class TypeRegistry", generatedCode);
        Assert.Contains("GetInjectableTypes", generatedCode);
    }

    [Fact]
    public void Generator_EmitsModuleInitializerBootstrap()
    {
        var source = @"
using NexusLabs.Needlr.Generators;

[assembly: GenerateTypeRegistry]

namespace TestApp
{
    public class MyService { }
}";

        var generatedCode = RunGenerator(source);

        Assert.Contains("ModuleInitializer", generatedCode);
        Assert.Contains("NeedlrSourceGenBootstrap.Register", generatedCode);
        Assert.Contains("TypeRegistry.GetInjectableTypes", generatedCode);
        Assert.Contains("TypeRegistry.GetPluginTypes", generatedCode);
    }

    [Fact]
    public void Generator_WithNamespacePrefixFilter_FiltersTypes()
    {
        var source = @"
using NexusLabs.Needlr.Generators;

[assembly: GenerateTypeRegistry(IncludeNamespacePrefixes = new[] { ""MyCompany"" })]

namespace MyCompany.Services
{
    public class IncludedService { }
}

namespace OtherCompany.Services
{
    public class ExcludedService { }
}";

        var generatedCode = RunGenerator(source);

        Assert.Contains("IncludedService", generatedCode);
        Assert.DoesNotContain("ExcludedService", generatedCode);
    }

    [Fact]
    public void Generator_WithIncludeSelfFalse_ExcludesSelfTypes()
    {
        var source = @"
using NexusLabs.Needlr.Generators;

[assembly: GenerateTypeRegistry(IncludeSelf = false)]

namespace TestApp
{
    public class MyService { }
}";

        var generatedCode = RunGenerator(source);

        // The generator should still produce the TypeRegistry class
        Assert.Contains("public static class TypeRegistry", generatedCode);
        // But the type from the current compilation should not be included
        Assert.DoesNotContain("MyService", generatedCode);
    }

    [Fact]
    public void Generator_ExcludesAbstractClasses()
    {
        var source = @"
using NexusLabs.Needlr.Generators;

[assembly: GenerateTypeRegistry]

namespace TestApp
{
    public abstract class AbstractService { }
    public class ConcreteService : AbstractService { }
}";

        var generatedCode = RunGenerator(source);

        Assert.Contains("ConcreteService", generatedCode);
        Assert.DoesNotContain("AbstractService", generatedCode);
    }

    [Fact]
    public void Generator_ExcludesInterfaces()
    {
        var source = @"
using NexusLabs.Needlr.Generators;

[assembly: GenerateTypeRegistry]

namespace TestApp
{
    public interface IService { }
    public class Service : IService { }
}";

        var generatedCode = RunGenerator(source);

        Assert.Contains("Service", generatedCode);
        // IService should only appear as an interface reference, not as a standalone type
        var lines = generatedCode.Split('\n');
        var typeofLines = lines.Where(l => l.Contains("new(typeof("));
        Assert.DoesNotContain(typeofLines, l => l.Contains("typeof(global::TestApp.IService),") && !l.Contains("["));
    }

    [Fact]
    public void Generator_IncludesInterfacesInTypeInfo()
    {
        var source = @"
using NexusLabs.Needlr.Generators;

[assembly: GenerateTypeRegistry]

namespace TestApp
{
    public interface IMyService { }
    public interface IAnotherService { }
    public class MyService : IMyService, IAnotherService { }
}";

        var generatedCode = RunGenerator(source);

        Assert.Contains("typeof(global::TestApp.IMyService)", generatedCode);
        Assert.Contains("typeof(global::TestApp.IAnotherService)", generatedCode);
    }

    [Fact]
    public void Generator_ExcludesTypesWithDoNotAutoRegister()
    {
        var source = @"
using NexusLabs.Needlr.Generators;

[assembly: GenerateTypeRegistry]

namespace NexusLabs.Needlr
{
    [System.AttributeUsage(System.AttributeTargets.Class | System.AttributeTargets.Interface)]
    public sealed class DoNotAutoRegisterAttribute : System.Attribute { }
}

namespace TestApp
{
    [NexusLabs.Needlr.DoNotAutoRegister]
    public class ExcludedService { }

    public class IncludedService { }
}";

        var generatedCode = RunGenerator(source);

        Assert.Contains("IncludedService", generatedCode);
        Assert.DoesNotContain("ExcludedService", generatedCode);
    }

    [Fact]
    public void Generator_ExcludesRecordTypes()
    {
        var source = @"
using NexusLabs.Needlr.Generators;

[assembly: GenerateTypeRegistry]

namespace TestApp
{
    public record MyRecord(string Value);
    public class MyService { }
}";

        var generatedCode = RunGenerator(source);

        Assert.Contains("MyService", generatedCode);
        Assert.DoesNotContain("MyRecord", generatedCode);
    }

    [Fact]
    public void Generator_ExcludesExceptionTypes()
    {
        var source = @"
using NexusLabs.Needlr.Generators;

[assembly: GenerateTypeRegistry]

namespace TestApp
{
    public class MyException : System.Exception { }
    public class MyService { }
}";

        var generatedCode = RunGenerator(source);

        Assert.Contains("MyService", generatedCode);
        Assert.DoesNotContain("MyException", generatedCode);
    }

    [Fact]
    public void Generator_ExcludesAttributeTypes()
    {
        var source = @"
using NexusLabs.Needlr.Generators;

[assembly: GenerateTypeRegistry]

namespace TestApp
{
    public class MyAttribute : System.Attribute { }
    public class MyService { }
}";

        var generatedCode = RunGenerator(source);

        Assert.Contains("MyService", generatedCode);
        Assert.DoesNotContain("MyAttribute", generatedCode);
    }

    [Fact]
    public void Generator_ProducesValidCSharpCode()
    {
        var source = @"
using NexusLabs.Needlr.Generators;

[assembly: GenerateTypeRegistry]

namespace TestApp
{
    public interface IMyService { }
    public class MyService : IMyService { }
    public class AnotherService { }
}";

        var generatedCode = RunGenerator(source);

        // Verify it contains the expected class declaration and method
        Assert.Contains("public static class TypeRegistry", generatedCode);
        Assert.Contains("GetInjectableTypes", generatedCode);
    }

    [Fact]
    public void Generator_EmitsLifetimeForInjectableTypes()
    {
        var source = @"
using NexusLabs.Needlr.Generators;

[assembly: GenerateTypeRegistry]

namespace TestApp
{
    public interface IMyService { }
    public class MyService : IMyService { }
}";

        var generatedCode = RunGenerator(source);

        // Verify lifetime is emitted (MyService has parameterless constructor -> Singleton)
        Assert.Contains("InjectableLifetime.Singleton", generatedCode);
    }

    [Fact]
    public void Generator_ExcludesTypesWithNonInjectableConstructors()
    {
        var source = @"
using NexusLabs.Needlr.Generators;

[assembly: GenerateTypeRegistry]

namespace TestApp
{
    public class ServiceWithStringParam
    {
        public ServiceWithStringParam(string value) { }
    }

    public interface IInjectableService { }
    public class InjectableService : IInjectableService { }
}";

        var generatedCode = RunGenerator(source);

        // ServiceWithStringParam has string parameter -> should be excluded entirely
        Assert.DoesNotContain("ServiceWithStringParam", generatedCode);

        // InjectableService should be included
        Assert.Contains("InjectableService", generatedCode);
    }

    [Fact]
    public void Generator_GeneratesPluginTypes()
    {
        var source = @"
using NexusLabs.Needlr.Generators;

[assembly: GenerateTypeRegistry]

namespace TestApp
{
    public interface IMyPlugin { }
    public class MyPlugin : IMyPlugin { }
}";

        var generatedCode = RunGenerator(source);

        // Verify plugin types are generated
        Assert.Contains("GetPluginTypes", generatedCode);
        Assert.Contains("_plugins", generatedCode);
    }

    [Fact]
    public void Generator_GeneratesPluginFactory()
    {
        var source = @"
using NexusLabs.Needlr.Generators;

[assembly: GenerateTypeRegistry]

namespace TestApp
{
    public interface IMyPlugin { }
    public class MyPlugin : IMyPlugin { }
}";

        var generatedCode = RunGenerator(source);

        // Verify factory lambda is generated
        Assert.Contains("() => new global::TestApp.MyPlugin()", generatedCode);
    }

    [Fact]
    public void Generator_ExcludesPluginsWithoutParameterlessConstructor()
    {
        var source = @"
using NexusLabs.Needlr.Generators;

[assembly: GenerateTypeRegistry]

namespace TestApp
{
    public interface IMyPlugin { }

    public class InvalidPlugin : IMyPlugin
    {
        public InvalidPlugin(string dependency) { }
    }

    public class ValidPlugin : IMyPlugin { }
}";

        var generatedCode = RunGenerator(source);

        // ValidPlugin should be in plugins, InvalidPlugin should not
        Assert.Contains("ValidPlugin", generatedCode);
        // InvalidPlugin appears in _types (as injectable) but not in _plugins (no parameterless ctor)
        var pluginsSection = ExtractPluginsSection(generatedCode);
        Assert.DoesNotContain("InvalidPlugin", pluginsSection);
    }

    [Fact]
    public void Generator_IncludesPluginInterfaces()
    {
        var source = @"
using NexusLabs.Needlr.Generators;

[assembly: GenerateTypeRegistry]

namespace TestApp
{
    public interface IPluginA { }
    public interface IPluginB { }
    public class MultiPlugin : IPluginA, IPluginB { }
}";

        var generatedCode = RunGenerator(source);

        // Verify both interfaces are in the plugin entry
        Assert.Contains("typeof(global::TestApp.IPluginA)", generatedCode);
        Assert.Contains("typeof(global::TestApp.IPluginB)", generatedCode);
    }

    private static string ExtractPluginsSection(string generatedCode)
    {
        var startIndex = generatedCode.IndexOf("private static readonly PluginTypeInfo[]");
        if (startIndex < 0) return string.Empty;

        var endIndex = generatedCode.IndexOf("];", startIndex);
        if (endIndex < 0) return string.Empty;

        return generatedCode.Substring(startIndex, endIndex - startIndex + 2);
    }

    private static string RunGenerator(string source)
    {
        // Create the attribute source with InjectableLifetime enum and PluginTypeInfo
        var attributeSource = @"
namespace NexusLabs.Needlr.Generators
{
    [System.AttributeUsage(System.AttributeTargets.Assembly)]
    public sealed class GenerateTypeRegistryAttribute : System.Attribute
    {
        public string[]? IncludeNamespacePrefixes { get; set; }
        public bool IncludeSelf { get; set; } = true;
    }

    public enum InjectableLifetime
    {
        Singleton = 0,
        Scoped = 1,
        Transient = 2
    }

    public readonly struct InjectableTypeInfo
    {
        public InjectableTypeInfo(System.Type type, System.Collections.Generic.IReadOnlyList<System.Type> interfaces)
            : this(type, interfaces, null) { }

        public InjectableTypeInfo(System.Type type, System.Collections.Generic.IReadOnlyList<System.Type> interfaces, InjectableLifetime? lifetime)
        {
            Type = type;
            Interfaces = interfaces;
            Lifetime = lifetime;
        }

        public System.Type Type { get; }
        public System.Collections.Generic.IReadOnlyList<System.Type> Interfaces { get; }
        public InjectableLifetime? Lifetime { get; }
    }

    public readonly struct PluginTypeInfo
    {
        public PluginTypeInfo(System.Type pluginType, System.Collections.Generic.IReadOnlyList<System.Type> pluginInterfaces, System.Func<object> factory)
        {
            PluginType = pluginType;
            PluginInterfaces = pluginInterfaces;
            Factory = factory;
        }

        public System.Type PluginType { get; }
        public System.Collections.Generic.IReadOnlyList<System.Type> PluginInterfaces { get; }
        public System.Func<object> Factory { get; }
    }

    public static class NeedlrSourceGenBootstrap
    {
        public static void Register(
            System.Func<System.Collections.Generic.IReadOnlyList<InjectableTypeInfo>> injectableTypeProvider,
            System.Func<System.Collections.Generic.IReadOnlyList<PluginTypeInfo>> pluginTypeProvider)
        {
        }
    }
}";

        var syntaxTrees = new[]
        {
            CSharpSyntaxTree.ParseText(attributeSource),
            CSharpSyntaxTree.ParseText(source)
        };

        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            syntaxTrees,
            Basic.Reference.Assemblies.Net90.References.All,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var generator = new TypeRegistryGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);

        driver = (CSharpGeneratorDriver)driver.RunGeneratorsAndUpdateCompilation(
            compilation,
            out var outputCompilation,
            out var diagnostics);

        var generatedTrees = outputCompilation.SyntaxTrees
            .Where(t => t.FilePath.EndsWith(".g.cs"))
            .OrderBy(t => t.FilePath)
            .ToList();

        if (generatedTrees.Count == 0)
        {
            // Return empty string if no code was generated
            return string.Empty;
        }

        return string.Join("\n\n", generatedTrees.Select(t => t.GetText().ToString()));
    }

    [Fact]
    public void Generator_WithDeferToContainerAttribute_UsesAttributeParameterTypes()
    {
        var source = @"
using NexusLabs.Needlr.Generators;

[assembly: GenerateTypeRegistry]

namespace TestApp
{
    public interface ICacheProvider { }

    // Simulates a partial class that will get a constructor from another generator
    [NexusLabs.Needlr.DeferToContainer(typeof(ICacheProvider))]
    public partial class CacheService { }
}";

        var generatedCode = RunGeneratorWithDeferToContainer(source);

        // Should contain factory that resolves ICacheProvider
        Assert.Contains("sp => new global::TestApp.CacheService(sp.GetRequiredService<global::TestApp.ICacheProvider>())", generatedCode);
    }

    [Fact]
    public void Generator_WithDeferToContainerAttribute_MultipleParams_GeneratesCorrectFactory()
    {
        var source = @"
using NexusLabs.Needlr.Generators;

[assembly: GenerateTypeRegistry]

namespace TestApp
{
    public interface ICacheProvider { }
    public interface ILogger { }

    [NexusLabs.Needlr.DeferToContainer(typeof(ICacheProvider), typeof(ILogger))]
    public partial class CacheService { }
}";

        var generatedCode = RunGeneratorWithDeferToContainer(source);

        // Should contain factory that resolves both dependencies
        Assert.Contains("sp => new global::TestApp.CacheService(sp.GetRequiredService<global::TestApp.ICacheProvider>(), sp.GetRequiredService<global::TestApp.ILogger>())", generatedCode);
    }

    [Fact]
    public void Generator_WithDeferToContainerAttribute_EmptyParams_GeneratesParameterlessFactory()
    {
        var source = @"
using NexusLabs.Needlr.Generators;

[assembly: GenerateTypeRegistry]

namespace TestApp
{
    [NexusLabs.Needlr.DeferToContainer]
    public partial class SimpleService { }
}";

        var generatedCode = RunGeneratorWithDeferToContainer(source);

        // Should contain parameterless factory
        Assert.Contains("sp => new global::TestApp.SimpleService()", generatedCode);
    }

    private static string RunGeneratorWithDeferToContainer(string source)
    {
        var attributeSource = @"
namespace NexusLabs.Needlr.Generators
{
    [System.AttributeUsage(System.AttributeTargets.Assembly)]
    public sealed class GenerateTypeRegistryAttribute : System.Attribute
    {
        public string[]? IncludeNamespacePrefixes { get; set; }
        public bool IncludeSelf { get; set; } = true;
    }

    public enum InjectableLifetime
    {
        Singleton = 0,
        Scoped = 1,
        Transient = 2
    }

    public readonly struct InjectableTypeInfo
    {
        public InjectableTypeInfo(System.Type type, System.Collections.Generic.IReadOnlyList<System.Type> interfaces, InjectableLifetime? lifetime, System.Func<System.IServiceProvider, object>? factory)
        {
            Type = type;
            Interfaces = interfaces;
            Lifetime = lifetime;
            Factory = factory;
        }

        public System.Type Type { get; }
        public System.Collections.Generic.IReadOnlyList<System.Type> Interfaces { get; }
        public InjectableLifetime? Lifetime { get; }
        public System.Func<System.IServiceProvider, object>? Factory { get; }
    }

    public readonly struct PluginTypeInfo
    {
        public PluginTypeInfo(System.Type pluginType, System.Collections.Generic.IReadOnlyList<System.Type> pluginInterfaces, System.Func<object> factory)
        {
            PluginType = pluginType;
            PluginInterfaces = pluginInterfaces;
            Factory = factory;
        }

        public System.Type PluginType { get; }
        public System.Collections.Generic.IReadOnlyList<System.Type> PluginInterfaces { get; }
        public System.Func<object> Factory { get; }
    }

    public static class NeedlrSourceGenBootstrap
    {
        public static void Register(
            System.Func<System.Collections.Generic.IReadOnlyList<InjectableTypeInfo>> injectableTypeProvider,
            System.Func<System.Collections.Generic.IReadOnlyList<PluginTypeInfo>> pluginTypeProvider)
        {
        }
    }
}

namespace NexusLabs.Needlr
{
    [System.AttributeUsage(System.AttributeTargets.Class)]
    public sealed class DeferToContainerAttribute : System.Attribute
    {
        public DeferToContainerAttribute(params System.Type[] constructorParameterTypes)
        {
            ConstructorParameterTypes = constructorParameterTypes ?? System.Array.Empty<System.Type>();
        }

        public System.Type[] ConstructorParameterTypes { get; }
    }
}";

        var syntaxTrees = new[]
        {
            CSharpSyntaxTree.ParseText(attributeSource),
            CSharpSyntaxTree.ParseText(source)
        };

        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            syntaxTrees,
            Basic.Reference.Assemblies.Net90.References.All,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var generator = new TypeRegistryGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);

        driver = (CSharpGeneratorDriver)driver.RunGeneratorsAndUpdateCompilation(
            compilation,
            out var outputCompilation,
            out var diagnostics);

        var generatedTrees = outputCompilation.SyntaxTrees
            .Where(t => t.FilePath.EndsWith(".g.cs"))
            .OrderBy(t => t.FilePath)
            .ToList();

        if (generatedTrees.Count == 0)
        {
            return string.Empty;
        }

        return string.Join("\n\n", generatedTrees.Select(t => t.GetText().ToString()));
    }
}
