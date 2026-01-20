using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

using Xunit;

#pragma warning disable xUnit1051 // Calls to methods which accept CancellationToken

namespace NexusLabs.Needlr.Generators.Tests;

/// <summary>
/// Tests for NDLR1002: Referenced assembly has internal plugin types but no type registry.
/// </summary>
public sealed class MissingTypeRegistryDiagnosticTests
{
    [Fact]
    public void Generator_EmitsDiagnostic_ForInternalPluginWithoutTypeRegistry()
    {
        // Create a "plugin" assembly with an internal plugin type but no [GenerateTypeRegistry]
        var pluginSource = @"
namespace TestPlugin
{
    public interface IServiceCollectionPlugin 
    { 
        void Configure(object options);
    }

    internal class MyPlugin : NexusLabs.Needlr.IServiceCollectionPlugin
    {
        public void Configure(NexusLabs.Needlr.ServiceCollectionPluginOptions options) { }
    }
}

namespace NexusLabs.Needlr
{
    public interface IServiceCollectionPlugin 
    { 
        void Configure(ServiceCollectionPluginOptions options);
    }
    
    public class ServiceCollectionPluginOptions { }
}";

        var pluginCompilation = CSharpCompilation.Create(
            "TestPlugin",
            [CSharpSyntaxTree.ParseText(pluginSource)],
            Basic.Reference.Assemblies.Net90.References.All,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        // Ensure no compilation errors
        var pluginErrors = pluginCompilation.GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToList();
        Assert.Empty(pluginErrors);

        // Create the "host" assembly with [GenerateTypeRegistry]
        var hostSource = @"
using NexusLabs.Needlr.Generators;

[assembly: GenerateTypeRegistry]

namespace HostApp
{
    public class HostService { }
}";

        var attributeSource = GetAttributeSource();

        var references = Basic.Reference.Assemblies.Net90.References.All
            .Cast<MetadataReference>()
            .Append(pluginCompilation.ToMetadataReference())
            .ToArray();

        var hostCompilation = CSharpCompilation.Create(
            "HostApp",
            [
                CSharpSyntaxTree.ParseText(attributeSource),
                CSharpSyntaxTree.ParseText(hostSource)
            ],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        // Run the generator
        var generator = new TypeRegistryGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);

        driver = (CSharpGeneratorDriver)driver.RunGeneratorsAndUpdateCompilation(
            hostCompilation,
            out _,
            out var diagnostics);

        // Assert - should have NDLR1002 error
        var ndlr1002Diagnostics = diagnostics
            .Where(d => d.Id == "NDLR1002")
            .ToList();

        Assert.NotEmpty(ndlr1002Diagnostics);
        Assert.Contains(ndlr1002Diagnostics, d => 
            d.GetMessage().Contains("MyPlugin") && 
            d.GetMessage().Contains("TestPlugin"));
        Assert.All(ndlr1002Diagnostics, d => 
            Assert.Equal(DiagnosticSeverity.Error, d.Severity));
    }

    [Fact]
    public void Generator_DoesNotEmitDiagnostic_ForPublicPlugin()
    {
        // Create a "plugin" assembly with a PUBLIC plugin type
        var pluginSource = @"
namespace TestPlugin
{
    public class MyPublicPlugin : NexusLabs.Needlr.IServiceCollectionPlugin
    {
        public void Configure(NexusLabs.Needlr.ServiceCollectionPluginOptions options) { }
    }
}

namespace NexusLabs.Needlr
{
    public interface IServiceCollectionPlugin 
    { 
        void Configure(ServiceCollectionPluginOptions options);
    }
    
    public class ServiceCollectionPluginOptions { }
}";

        var pluginCompilation = CSharpCompilation.Create(
            "TestPlugin",
            [CSharpSyntaxTree.ParseText(pluginSource)],
            Basic.Reference.Assemblies.Net90.References.All,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        // Create the "host" assembly with [GenerateTypeRegistry]
        var hostSource = @"
using NexusLabs.Needlr.Generators;

[assembly: GenerateTypeRegistry]

namespace HostApp
{
    public class HostService { }
}";

        var attributeSource = GetAttributeSource();

        var references = Basic.Reference.Assemblies.Net90.References.All
            .Cast<MetadataReference>()
            .Append(pluginCompilation.ToMetadataReference())
            .ToArray();

        var hostCompilation = CSharpCompilation.Create(
            "HostApp",
            [
                CSharpSyntaxTree.ParseText(attributeSource),
                CSharpSyntaxTree.ParseText(hostSource)
            ],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        // Run the generator
        var generator = new TypeRegistryGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);

        driver = (CSharpGeneratorDriver)driver.RunGeneratorsAndUpdateCompilation(
            hostCompilation,
            out _,
            out var diagnostics);

        // Assert - should NOT have NDLR1002 error (public plugins are fine)
        var ndlr1002Diagnostics = diagnostics
            .Where(d => d.Id == "NDLR1002")
            .ToList();

        Assert.Empty(ndlr1002Diagnostics);
    }

    [Fact]
    public void Generator_DoesNotEmitDiagnostic_WhenPluginAssemblyHasTypeRegistry()
    {
        // Create a "plugin" assembly WITH [GenerateTypeRegistry]
        var pluginSource = @"
using NexusLabs.Needlr.Generators;

[assembly: GenerateTypeRegistry]

namespace TestPlugin
{
    internal class MyPlugin : NexusLabs.Needlr.IServiceCollectionPlugin
    {
        public void Configure(NexusLabs.Needlr.ServiceCollectionPluginOptions options) { }
    }
}

namespace NexusLabs.Needlr
{
    public interface IServiceCollectionPlugin 
    { 
        void Configure(ServiceCollectionPluginOptions options);
    }
    
    public class ServiceCollectionPluginOptions { }
}

namespace NexusLabs.Needlr.Generators
{
    [System.AttributeUsage(System.AttributeTargets.Assembly)]
    public sealed class GenerateTypeRegistryAttribute : System.Attribute
    {
        public string[]? IncludeNamespacePrefixes { get; set; }
        public bool IncludeSelf { get; set; } = true;
    }
}";

        var pluginCompilation = CSharpCompilation.Create(
            "TestPlugin",
            [CSharpSyntaxTree.ParseText(pluginSource)],
            Basic.Reference.Assemblies.Net90.References.All,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        // Create the "host" assembly with [GenerateTypeRegistry]
        var hostSource = @"
using NexusLabs.Needlr.Generators;

[assembly: GenerateTypeRegistry]

namespace HostApp
{
    public class HostService { }
}";

        var attributeSource = GetAttributeSource();

        var references = Basic.Reference.Assemblies.Net90.References.All
            .Cast<MetadataReference>()
            .Append(pluginCompilation.ToMetadataReference())
            .ToArray();

        var hostCompilation = CSharpCompilation.Create(
            "HostApp",
            [
                CSharpSyntaxTree.ParseText(attributeSource),
                CSharpSyntaxTree.ParseText(hostSource)
            ],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        // Run the generator
        var generator = new TypeRegistryGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);

        driver = (CSharpGeneratorDriver)driver.RunGeneratorsAndUpdateCompilation(
            hostCompilation,
            out _,
            out var diagnostics);

        // Assert - should NOT have NDLR1002 error (assembly has [GenerateTypeRegistry])
        var ndlr1002Diagnostics = diagnostics
            .Where(d => d.Id == "NDLR1002")
            .ToList();

        Assert.Empty(ndlr1002Diagnostics);
    }

    [Fact]
    public void Generator_DoesNotEmitDiagnostic_ForInternalNonPluginType()
    {
        // Create a "plugin" assembly with internal types that are NOT plugins
        var pluginSource = @"
namespace TestPlugin
{
    public interface IMyService { }
    internal class MyInternalService : IMyService { }
}";

        var pluginCompilation = CSharpCompilation.Create(
            "TestPlugin",
            [CSharpSyntaxTree.ParseText(pluginSource)],
            Basic.Reference.Assemblies.Net90.References.All,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        // Create the "host" assembly with [GenerateTypeRegistry]
        var hostSource = @"
using NexusLabs.Needlr.Generators;

[assembly: GenerateTypeRegistry]

namespace HostApp
{
    public class HostService { }
}";

        var attributeSource = GetAttributeSource();

        var references = Basic.Reference.Assemblies.Net90.References.All
            .Cast<MetadataReference>()
            .Append(pluginCompilation.ToMetadataReference())
            .ToArray();

        var hostCompilation = CSharpCompilation.Create(
            "HostApp",
            [
                CSharpSyntaxTree.ParseText(attributeSource),
                CSharpSyntaxTree.ParseText(hostSource)
            ],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        // Run the generator
        var generator = new TypeRegistryGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);

        driver = (CSharpGeneratorDriver)driver.RunGeneratorsAndUpdateCompilation(
            hostCompilation,
            out _,
            out var diagnostics);

        // Assert - should NOT have NDLR1002 error (not a plugin type)
        var ndlr1002Diagnostics = diagnostics
            .Where(d => d.Id == "NDLR1002")
            .ToList();

        Assert.Empty(ndlr1002Diagnostics);
    }

    private static string GetAttributeSource() => @"
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
}
