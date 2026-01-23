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
    public void Generator_WithEmptyStringPrefix_IncludesGlobalNamespaceTypes()
    {
        var source = @"
using NexusLabs.Needlr.Generators;

// Empty string prefix means include global namespace types
[assembly: GenerateTypeRegistry(IncludeNamespacePrefixes = new[] { ""MyCompany"", """" })]

namespace MyCompany.Services
{
    public class NamespacedService { }
}

// This class is in the global namespace
public class GlobalService { }
";

        var generatedCode = RunGenerator(source);

        Assert.Contains("NamespacedService", generatedCode);
        Assert.Contains("GlobalService", generatedCode);
    }

    [Fact]
    public void Generator_WithoutEmptyStringPrefix_ExcludesGlobalNamespaceTypes()
    {
        var source = @"
using NexusLabs.Needlr.Generators;

// No empty string prefix, so global namespace types should be excluded
[assembly: GenerateTypeRegistry(IncludeNamespacePrefixes = new[] { ""MyCompany"" })]

namespace MyCompany.Services
{
    public class NamespacedService { }
}

// This class is in the global namespace - should NOT be included
public class GlobalService { }
";

        var generatedCode = RunGenerator(source);

        Assert.Contains("NamespacedService", generatedCode);
        Assert.DoesNotContain("GlobalService", generatedCode);
    }

    [Fact]
    public void Generator_WithOnlyEmptyStringPrefix_IncludesOnlyGlobalNamespaceTypes()
    {
        var source = @"
using NexusLabs.Needlr.Generators;

// Only empty string prefix - only global namespace types
[assembly: GenerateTypeRegistry(IncludeNamespacePrefixes = new[] { """" })]

namespace MyCompany.Services
{
    public class NamespacedService { }
}

// This class is in the global namespace
public class GlobalService { }
";

        var generatedCode = RunGenerator(source);

        Assert.DoesNotContain("NamespacedService", generatedCode);
        Assert.Contains("GlobalService", generatedCode);
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
        // AbstractService should not be registered as a standalone injectable type
        // but it MAY appear as a base type reference for ConcreteService
        // Check that it's not in the injectable types array directly
        Assert.DoesNotContain("new InjectableTypeInfo(typeof(global::TestApp.AbstractService)", generatedCode);
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
    public void Generator_OpenGenericType_IsExcludedFromGeneration()
    {
        var source = @"
using NexusLabs.Needlr.Generators;

[assembly: GenerateTypeRegistry]

namespace TestApp
{
    public interface IJob { }
    
    // Open generic type - should NOT be included in generated code
    public class JobScheduler<TJob> where TJob : IJob
    {
        public JobScheduler() { }
    }
    
    // Regular class - should be included
    public class RegularService { }
}";

        var generatedCode = RunGenerator(source);

        // Should contain the regular service
        Assert.Contains("RegularService", generatedCode);
        // Should NOT contain the generic type with type parameter
        Assert.DoesNotContain("JobScheduler<TJob>", generatedCode);
        Assert.DoesNotContain("JobScheduler`1", generatedCode);
    }

    [Fact]
    public void Generator_MultipleTypeParameterGeneric_IsExcludedFromGeneration()
    {
        var source = @"
using NexusLabs.Needlr.Generators;

[assembly: GenerateTypeRegistry]

namespace TestApp
{
    // Open generic type with multiple parameters - should NOT be included
    public class Repository<TEntity, TKey>
    {
        public Repository() { }
    }
    
    // Regular class - should be included
    public class ConcreteRepository { }
}";

        var generatedCode = RunGenerator(source);

        // Should contain the concrete type
        Assert.Contains("ConcreteRepository", generatedCode);
        // Should NOT contain the generic type
        Assert.DoesNotContain("Repository<TEntity", generatedCode);
        Assert.DoesNotContain("Repository`2", generatedCode);
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

    [Fact]
    public void Generator_WithDeferToContainerAttribute_AndPrimaryConstructor_UsesAttributeNotConstructor()
    {
        // This mimics the real scenario: a class has BOTH [DeferToContainer] AND a primary constructor
        // The generator should use the attribute types, not the visible constructor
        var source = @"
using NexusLabs.Needlr.Generators;

[assembly: GenerateTypeRegistry]

namespace TestApp
{
    public interface ICacheProvider { }

    // Class has primary constructor AND DeferToContainer attribute
    // Generator should use attribute (typeof(ICacheProvider)), not the actual constructor
    [NexusLabs.Needlr.DeferToContainer(typeof(ICacheProvider))]
    public sealed partial class EngageFeedCacheProvider(ICacheProvider _cacheProvider)
    {
    }
}";

        var generatedCode = RunGeneratorWithDeferToContainer(source);

        // Should use the DeferToContainer parameter, producing a factory with ICacheProvider
        Assert.Contains("sp => new global::TestApp.EngageFeedCacheProvider(sp.GetRequiredService<global::TestApp.ICacheProvider>())", generatedCode);
        // Should NOT produce parameterless factory
        Assert.DoesNotContain("sp => new global::TestApp.EngageFeedCacheProvider()", generatedCode);
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

#pragma warning disable xUnit1051 // Using CancellationToken with synchronous compilation methods
    [Fact]
    public void Generator_WithDeferToContainerFromReferencedAssembly_UsesAttributeParameterTypes()
    {
        // Create a compilation for the "NexusLabs.Needlr" assembly that contains DeferToContainerAttribute
        var needlrSource = @"
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
        var needlrCompilation = CSharpCompilation.Create(
            "NexusLabs.Needlr",
            new[] { CSharpSyntaxTree.ParseText(needlrSource) },
            Basic.Reference.Assemblies.Net90.References.All,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var needlrAssemblyStream = new System.IO.MemoryStream();
        var needlrEmitResult = needlrCompilation.Emit(needlrAssemblyStream);
        Assert.True(needlrEmitResult.Success, "Failed to emit NexusLabs.Needlr assembly");
        needlrAssemblyStream.Position = 0;
        var needlrReference = MetadataReference.CreateFromStream(needlrAssemblyStream);

        // Create source for the generator attributes
        var generatorAttributeSource = @"
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
}";
        // Consumer app source - references NexusLabs.Needlr.DeferToContainer from external assembly
        var appSource = @"
using NexusLabs.Needlr.Generators;

[assembly: GenerateTypeRegistry]

namespace TestApp
{
    public interface ICacheProvider { }

    [NexusLabs.Needlr.DeferToContainer(typeof(ICacheProvider))]
    public sealed partial class EngageFeedCacheProvider(ICacheProvider _cacheProvider)
    {
    }
}";
        var syntaxTrees = new[]
        {
            CSharpSyntaxTree.ParseText(generatorAttributeSource),
            CSharpSyntaxTree.ParseText(appSource)
        };

        var references = Basic.Reference.Assemblies.Net90.References.All.Append(needlrReference).ToList();

        var compilation = CSharpCompilation.Create(
            "TestApp",
            syntaxTrees,
            references,
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

        var generatedCode = string.Join("\n\n", generatedTrees.Select(t => t.GetText().ToString()));

        // Should use the DeferToContainer parameter, producing a factory with ICacheProvider
        Assert.Contains("sp => new global::TestApp.EngageFeedCacheProvider(sp.GetRequiredService<global::TestApp.ICacheProvider>())", generatedCode);
        // Should NOT produce parameterless factory
        Assert.DoesNotContain("sp => new global::TestApp.EngageFeedCacheProvider()", generatedCode);
    }
#pragma warning restore xUnit1051

    #region Assembly Force-Loading Tests

    [Fact]
    public void Generator_WithReferencedAssemblyHavingGenerateTypeRegistry_GeneratesForceLoadMethod()
    {
        // This test simulates having a referenced assembly with [GenerateTypeRegistry]
        // We need to create a fake referenced assembly that has the attribute
        
        var referencedAssemblySource = @"
using NexusLabs.Needlr.Generators;

[assembly: GenerateTypeRegistry(IncludeNamespacePrefixes = new[] { ""ReferencedLib"" })]

namespace ReferencedLib
{
    public class ReferencedService { }
}";

        var mainAssemblySource = @"
using NexusLabs.Needlr.Generators;

[assembly: GenerateTypeRegistry(IncludeNamespacePrefixes = new[] { ""MainApp"" })]

namespace MainApp
{
    public class MainService { }
}";

        var (_, mainGeneratedCode) = RunGeneratorWithReferencedAssembly(referencedAssemblySource, mainAssemblySource);

        // Should generate ForceLoadReferencedAssemblies method
        Assert.Contains("ForceLoadReferencedAssemblies", mainGeneratedCode);
        Assert.Contains("MethodImpl(MethodImplOptions.NoInlining)", mainGeneratedCode);
        Assert.Contains("typeof(global::ReferencedLib.Generated.TypeRegistry).Assembly", mainGeneratedCode);
    }

    [Fact]
    public void Generator_WithNoReferencedAssembliesHavingGenerateTypeRegistry_DoesNotGenerateForceLoadMethod()
    {
        var source = @"
using NexusLabs.Needlr.Generators;

[assembly: GenerateTypeRegistry(IncludeNamespacePrefixes = new[] { ""TestApp"" })]

namespace TestApp
{
    public class MyService { }
}";

        var generatedCode = RunGenerator(source);

        // Should NOT generate ForceLoadReferencedAssemblies method when no referenced assemblies have [GenerateTypeRegistry]
        Assert.DoesNotContain("ForceLoadReferencedAssemblies", generatedCode);
    }

    [Fact]
    public void Generator_WithNeedlrAssemblyOrderFirst_GeneratesAssembliesInCorrectOrder()
    {
        var referencedAssembly1Source = @"
using NexusLabs.Needlr.Generators;

[assembly: GenerateTypeRegistry(IncludeNamespacePrefixes = new[] { ""Lib.Alpha"" })]

namespace Lib.Alpha
{
    public class AlphaService { }
}";

        var referencedAssembly2Source = @"
using NexusLabs.Needlr.Generators;

[assembly: GenerateTypeRegistry(IncludeNamespacePrefixes = new[] { ""Lib.Zulu"" })]

namespace Lib.Zulu
{
    public class ZuluService { }
}";

        var mainAssemblySource = @"
using NexusLabs.Needlr.Generators;

[assembly: GenerateTypeRegistry(IncludeNamespacePrefixes = new[] { ""MainApp"" })]
[assembly: NeedlrAssemblyOrder(First = new[] { ""Lib.Zulu"" })]

namespace MainApp
{
    public class MainService { }
}";

        var (_, mainGeneratedCode) = RunGeneratorWithMultipleReferencedAssemblies(
            new[] { ("Lib.Alpha", referencedAssembly1Source), ("Lib.Zulu", referencedAssembly2Source) },
            mainAssemblySource,
            "MainApp");

        // Zulu should appear BEFORE Alpha in the generated code (First takes precedence over alphabetical)
        var zuluIndex = mainGeneratedCode.IndexOf("Lib.Zulu.Generated.TypeRegistry");
        var alphaIndex = mainGeneratedCode.IndexOf("Lib.Alpha.Generated.TypeRegistry");
        
        Assert.True(zuluIndex >= 0, "Lib.Zulu should be in the generated code");
        Assert.True(alphaIndex >= 0, "Lib.Alpha should be in the generated code");
        Assert.True(zuluIndex < alphaIndex, "Lib.Zulu (in First) should come before Lib.Alpha (alphabetical)");
    }

    [Fact]
    public void Generator_WithNeedlrAssemblyOrderLast_GeneratesAssembliesInCorrectOrder()
    {
        var referencedAssembly1Source = @"
using NexusLabs.Needlr.Generators;

[assembly: GenerateTypeRegistry(IncludeNamespacePrefixes = new[] { ""Lib.Alpha"" })]

namespace Lib.Alpha
{
    public class AlphaService { }
}";

        var referencedAssembly2Source = @"
using NexusLabs.Needlr.Generators;

[assembly: GenerateTypeRegistry(IncludeNamespacePrefixes = new[] { ""Lib.Beta"" })]

namespace Lib.Beta
{
    public class BetaService { }
}";

        var mainAssemblySource = @"
using NexusLabs.Needlr.Generators;

[assembly: GenerateTypeRegistry(IncludeNamespacePrefixes = new[] { ""MainApp"" })]
[assembly: NeedlrAssemblyOrder(Last = new[] { ""Lib.Alpha"" })]

namespace MainApp
{
    public class MainService { }
}";

        var (_, mainGeneratedCode) = RunGeneratorWithMultipleReferencedAssemblies(
            new[] { ("Lib.Alpha", referencedAssembly1Source), ("Lib.Beta", referencedAssembly2Source) },
            mainAssemblySource,
            "MainApp");

        // Alpha should appear AFTER Beta (Last takes precedence, Beta is alphabetically before Alpha without override)
        var alphaIndex = mainGeneratedCode.IndexOf("Lib.Alpha.Generated.TypeRegistry");
        var betaIndex = mainGeneratedCode.IndexOf("Lib.Beta.Generated.TypeRegistry");
        
        Assert.True(alphaIndex >= 0, "Lib.Alpha should be in the generated code");
        Assert.True(betaIndex >= 0, "Lib.Beta should be in the generated code");
        Assert.True(betaIndex < alphaIndex, "Lib.Beta (alphabetical) should come before Lib.Alpha (in Last)");
    }

    [Fact]
    public void Generator_WithNeedlrAssemblyOrderFirstAndLast_GeneratesAssembliesInCorrectOrder()
    {
        var assembly1Source = @"
using NexusLabs.Needlr.Generators;

[assembly: GenerateTypeRegistry(IncludeNamespacePrefixes = new[] { ""Lib.Alpha"" })]

namespace Lib.Alpha
{
    public class AlphaService { }
}";

        var assembly2Source = @"
using NexusLabs.Needlr.Generators;

[assembly: GenerateTypeRegistry(IncludeNamespacePrefixes = new[] { ""Lib.Beta"" })]

namespace Lib.Beta
{
    public class BetaService { }
}";

        var assembly3Source = @"
using NexusLabs.Needlr.Generators;

[assembly: GenerateTypeRegistry(IncludeNamespacePrefixes = new[] { ""Lib.Gamma"" })]

namespace Lib.Gamma
{
    public class GammaService { }
}";

        var mainAssemblySource = @"
using NexusLabs.Needlr.Generators;

[assembly: GenerateTypeRegistry(IncludeNamespacePrefixes = new[] { ""MainApp"" })]
[assembly: NeedlrAssemblyOrder(First = new[] { ""Lib.Gamma"" }, Last = new[] { ""Lib.Alpha"" })]

namespace MainApp
{
    public class MainService { }
}";

        var (_, mainGeneratedCode) = RunGeneratorWithMultipleReferencedAssemblies(
            new[] { ("Lib.Alpha", assembly1Source), ("Lib.Beta", assembly2Source), ("Lib.Gamma", assembly3Source) },
            mainAssemblySource,
            "MainApp");

        // Order should be: Gamma (First), Beta (alphabetical middle), Alpha (Last)
        var gammaIndex = mainGeneratedCode.IndexOf("Lib.Gamma.Generated.TypeRegistry");
        var betaIndex = mainGeneratedCode.IndexOf("Lib.Beta.Generated.TypeRegistry");
        var alphaIndex = mainGeneratedCode.IndexOf("Lib.Alpha.Generated.TypeRegistry");
        
        Assert.True(gammaIndex >= 0, "Lib.Gamma should be in the generated code");
        Assert.True(betaIndex >= 0, "Lib.Beta should be in the generated code");
        Assert.True(alphaIndex >= 0, "Lib.Alpha should be in the generated code");
        Assert.True(gammaIndex < betaIndex, "Lib.Gamma (First) should come before Lib.Beta");
        Assert.True(betaIndex < alphaIndex, "Lib.Beta should come before Lib.Alpha (Last)");
    }

    [Fact]
    public void Generator_WithNeedlrAssemblyOrderPresetTestsLast_OrdersTestAssembliesLast()
    {
        var assembly1Source = @"
using NexusLabs.Needlr.Generators;

[assembly: GenerateTypeRegistry(IncludeNamespacePrefixes = new[] { ""Lib.Core"" })]

namespace Lib.Core
{
    public class CoreService { }
}";

        var assembly2Source = @"
using NexusLabs.Needlr.Generators;

[assembly: GenerateTypeRegistry(IncludeNamespacePrefixes = new[] { ""Lib.Core.Tests"" })]

namespace Lib.Core.Tests
{
    public class TestService { }
}";

        var assembly3Source = @"
using NexusLabs.Needlr.Generators;

[assembly: GenerateTypeRegistry(IncludeNamespacePrefixes = new[] { ""Lib.Features"" })]

namespace Lib.Features
{
    public class FeatureService { }
}";

        var mainAssemblySource = @"
using NexusLabs.Needlr.Generators;

[assembly: GenerateTypeRegistry(IncludeNamespacePrefixes = new[] { ""MainApp"" })]
[assembly: NeedlrAssemblyOrder(Preset = 1)]

namespace MainApp
{
    public class MainService { }
}";

        var (_, mainGeneratedCode) = RunGeneratorWithMultipleReferencedAssemblies(
            new[] { ("Lib.Core", assembly1Source), ("Lib.Core.Tests", assembly2Source), ("Lib.Features", assembly3Source) },
            mainAssemblySource,
            "MainApp");

        // Order should be: Core, Features (non-tests alphabetically), then Core.Tests (tests)
        var coreIndex = mainGeneratedCode.IndexOf("Lib.Core.Generated.TypeRegistry");
        var testsIndex = mainGeneratedCode.IndexOf("Lib.Core.Tests.Generated.TypeRegistry");
        var featuresIndex = mainGeneratedCode.IndexOf("Lib.Features.Generated.TypeRegistry");
        
        Assert.True(coreIndex >= 0, "Lib.Core should be in the generated code");
        Assert.True(testsIndex >= 0, "Lib.Core.Tests should be in the generated code");
        Assert.True(featuresIndex >= 0, "Lib.Features should be in the generated code");
        Assert.True(coreIndex < featuresIndex, "Lib.Core should come before Lib.Features (alphabetical non-tests)");
        Assert.True(featuresIndex < testsIndex, "Lib.Features should come before Lib.Core.Tests (tests come last)");
    }

    [Fact]
    public void Generator_ForceLoadIncludesRuntimeCompilerServicesUsing()
    {
        var referencedAssemblySource = @"
using NexusLabs.Needlr.Generators;

[assembly: GenerateTypeRegistry(IncludeNamespacePrefixes = new[] { ""ReferencedLib"" })]

namespace ReferencedLib
{
    public class ReferencedService { }
}";

        var mainAssemblySource = @"
using NexusLabs.Needlr.Generators;

[assembly: GenerateTypeRegistry(IncludeNamespacePrefixes = new[] { ""MainApp"" })]

namespace MainApp
{
    public class MainService { }
}";

        var (_, mainGeneratedCode) = RunGeneratorWithReferencedAssembly(referencedAssemblySource, mainAssemblySource);

        // Should include the using for MethodImpl attribute
        Assert.Contains("using System.Runtime.CompilerServices;", mainGeneratedCode);
    }

    private (string referencedGeneratedCode, string mainGeneratedCode) RunGeneratorWithReferencedAssembly(
        string referencedSource, 
        string mainSource)
    {
        var attributeSource = GetStandardAttributeSource();

        // Step 1: Run generator on the referenced library (don't emit, just get generated code)
        var referencedSyntaxTrees = new[]
        {
            CSharpSyntaxTree.ParseText(attributeSource),
            CSharpSyntaxTree.ParseText(referencedSource)
        };

        var referencedCompilation = CSharpCompilation.Create(
            "ReferencedLib",
            referencedSyntaxTrees,
            Basic.Reference.Assemblies.Net90.References.All,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var referencedGenerator = new TypeRegistryGenerator();
        var referencedDriver = CSharpGeneratorDriver.Create(referencedGenerator);

        referencedDriver = (CSharpGeneratorDriver)referencedDriver.RunGeneratorsAndUpdateCompilation(
            referencedCompilation,
            out var referencedOutputCompilation,
            out _);

        var referencedGeneratedCode = string.Join("\n\n", 
            referencedOutputCompilation.SyntaxTrees
                .Where(t => t.FilePath.EndsWith(".g.cs"))
                .Select(t => t.GetText().ToString()));

        // Step 2: Create a fake metadata reference that represents the referenced assembly with [GenerateTypeRegistry]
        // We use the original compilation (before generation) to create a reference, then manually inject the attribute
        var referencedAssemblyStream = new System.IO.MemoryStream();
        var emitResult = referencedCompilation.Emit(referencedAssemblyStream);
        Assert.True(emitResult.Success, $"Referenced library compilation failed: {string.Join(", ", emitResult.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error))}");
        referencedAssemblyStream.Position = 0;
        var referencedMetadataReference = MetadataReference.CreateFromStream(referencedAssemblyStream);

        // Step 3: Compile the main assembly with the referenced library
        var mainSyntaxTrees = new[]
        {
            CSharpSyntaxTree.ParseText(attributeSource),
            CSharpSyntaxTree.ParseText(mainSource)
        };

        var mainCompilation = CSharpCompilation.Create(
            "MainApp",
            mainSyntaxTrees,
            Basic.Reference.Assemblies.Net90.References.All.Append(referencedMetadataReference),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var mainGenerator = new TypeRegistryGenerator();
        var mainDriver = CSharpGeneratorDriver.Create(mainGenerator);

        mainDriver = (CSharpGeneratorDriver)mainDriver.RunGeneratorsAndUpdateCompilation(
            mainCompilation,
            out var mainOutputCompilation,
            out _);

        var mainGeneratedCode = string.Join("\n\n", 
            mainOutputCompilation.SyntaxTrees
                .Where(t => t.FilePath.EndsWith(".g.cs"))
                .Select(t => t.GetText().ToString()));

        return (referencedGeneratedCode, mainGeneratedCode);
    }

    private (string[] referencedGeneratedCodes, string mainGeneratedCode) RunGeneratorWithMultipleReferencedAssemblies(
        (string assemblyName, string source)[] referencedAssemblies,
        string mainSource,
        string mainAssemblyName)
    {
        var attributeSource = GetStandardAttributeSource();
        var referencedCodes = new List<string>();
        var metadataReferences = new List<MetadataReference>(Basic.Reference.Assemblies.Net90.References.All);

        foreach (var (assemblyName, source) in referencedAssemblies)
        {
            var syntaxTrees = new[]
            {
                CSharpSyntaxTree.ParseText(attributeSource),
                CSharpSyntaxTree.ParseText(source)
            };

            // Compile without generator to create a minimal reference assembly
            var compilation = CSharpCompilation.Create(
                assemblyName,
                syntaxTrees,
                Basic.Reference.Assemblies.Net90.References.All,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            // Run generator to get the generated code (for inspection)
            var generator = new TypeRegistryGenerator();
            var driver = CSharpGeneratorDriver.Create(generator);

            driver = (CSharpGeneratorDriver)driver.RunGeneratorsAndUpdateCompilation(
                compilation,
                out var outputCompilation,
                out _);

            var generatedCode = string.Join("\n\n", 
                outputCompilation.SyntaxTrees
                    .Where(t => t.FilePath.EndsWith(".g.cs"))
                    .Select(t => t.GetText().ToString()));
            referencedCodes.Add(generatedCode);

            // Create reference from original compilation (without generated code)
            var stream = new System.IO.MemoryStream();
            var emitResult = compilation.Emit(stream);
            Assert.True(emitResult.Success, $"Referenced library {assemblyName} compilation failed: {string.Join(", ", emitResult.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error))}");
            stream.Position = 0;
            metadataReferences.Add(MetadataReference.CreateFromStream(stream));
        }

        // Compile main assembly
        var mainSyntaxTrees = new[]
        {
            CSharpSyntaxTree.ParseText(attributeSource),
            CSharpSyntaxTree.ParseText(mainSource)
        };

        var mainCompilation = CSharpCompilation.Create(
            mainAssemblyName,
            mainSyntaxTrees,
            metadataReferences,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var mainGenerator = new TypeRegistryGenerator();
        var mainDriver = CSharpGeneratorDriver.Create(mainGenerator);

        mainDriver = (CSharpGeneratorDriver)mainDriver.RunGeneratorsAndUpdateCompilation(
            mainCompilation,
            out var mainOutputCompilation,
            out _);

        var mainGeneratedCode = string.Join("\n\n", 
            mainOutputCompilation.SyntaxTrees
                .Where(t => t.FilePath.EndsWith(".g.cs"))
                .Select(t => t.GetText().ToString()));

        return (referencedCodes.ToArray(), mainGeneratedCode);
    }

    private static string GetStandardAttributeSource() => @"
namespace NexusLabs.Needlr.Generators
{
    [System.AttributeUsage(System.AttributeTargets.Assembly)]
    public sealed class GenerateTypeRegistryAttribute : System.Attribute
    {
        public string[]? IncludeNamespacePrefixes { get; set; }
        public bool IncludeSelf { get; set; } = true;
    }

    [System.AttributeUsage(System.AttributeTargets.Assembly)]
    public sealed class NeedlrAssemblyOrderAttribute : System.Attribute
    {
        public int Preset { get; set; } = 0;
        public string[]? First { get; set; }
        public string[]? Last { get; set; }
    }

    public enum AssemblyOrderPreset
    {
        None = 0,
        TestsLast = 1,
        Alphabetical = 2
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

    #endregion
}
