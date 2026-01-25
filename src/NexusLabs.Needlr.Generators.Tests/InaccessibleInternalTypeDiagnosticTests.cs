using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

using System.Collections.Immutable;

using Xunit;

#pragma warning disable xUnit1051 // Calls to methods which accept CancellationToken

namespace NexusLabs.Needlr.Generators.Tests;

/// <summary>
/// Tests for NDLRGEN001: Internal type in referenced assembly cannot be registered.
/// </summary>
public sealed class InaccessibleInternalTypeDiagnosticTests
{
    [Fact]
    public void Generator_EmitsDiagnostic_ForInternalTypeInReferencedAssembly()
    {
        // First, create a "referenced" assembly with an internal type
        var referencedSource = @"
namespace ReferencedLib
{
    public interface IService { }
    internal class InternalService : IService { }
}";

        var referencedCompilation = CSharpCompilation.Create(
            "ReferencedLib",
            [CSharpSyntaxTree.ParseText(referencedSource)],
            Basic.Reference.Assemblies.Net100.References.All,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        // Ensure the referenced compilation has no errors
        var referencedErrors = referencedCompilation.GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToList();
        Assert.Empty(referencedErrors);

        // Create the "host" assembly that references it and tries to scan the namespace
        var hostSource = @"
using NexusLabs.Needlr.Generators;

[assembly: GenerateTypeRegistry(IncludeNamespacePrefixes = new[] { ""ReferencedLib"" })]

namespace HostApp
{
    public class HostService { }
}";

        var attributeSource = GetAttributeSource();

        var references = Basic.Reference.Assemblies.Net100.References.All
            .Cast<MetadataReference>()
            .Append(referencedCompilation.ToMetadataReference())
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

        // Assert - should have NDLRGEN001 error
        var ndlrgen001Diagnostics = diagnostics
            .Where(d => d.Id == "NDLRGEN001")
            .ToList();

        Assert.NotEmpty(ndlrgen001Diagnostics);
        Assert.Contains(ndlrgen001Diagnostics, d => 
            d.GetMessage().Contains("InternalService") && 
            d.GetMessage().Contains("ReferencedLib"));
        Assert.All(ndlrgen001Diagnostics, d => 
            Assert.Equal(DiagnosticSeverity.Error, d.Severity));
    }

    [Fact]
    public void Generator_DoesNotEmitDiagnostic_ForPublicTypeInReferencedAssembly()
    {
        // Create a "referenced" assembly with a public type
        var referencedSource = @"
namespace ReferencedLib
{
    public interface IService { }
    public class PublicService : IService { }
}";

        var referencedCompilation = CSharpCompilation.Create(
            "ReferencedLib",
            [CSharpSyntaxTree.ParseText(referencedSource)],
            Basic.Reference.Assemblies.Net100.References.All,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        // Create the "host" assembly that references it
        var hostSource = @"
using NexusLabs.Needlr.Generators;

[assembly: GenerateTypeRegistry(IncludeNamespacePrefixes = new[] { ""ReferencedLib"" })]

namespace HostApp
{
    public class HostService { }
}";

        var attributeSource = GetAttributeSource();

        var references = Basic.Reference.Assemblies.Net100.References.All
            .Cast<MetadataReference>()
            .Append(referencedCompilation.ToMetadataReference())
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

        // Assert - should NOT have NDLRGEN001 error
        var ndlrgen001Diagnostics = diagnostics
            .Where(d => d.Id == "NDLRGEN001")
            .ToList();

        Assert.Empty(ndlrgen001Diagnostics);
    }

    [Fact]
    public void Generator_DoesNotEmitDiagnostic_ForInternalTypeInCurrentAssembly()
    {
        // Internal types in the current assembly should be fine
        var source = @"
using NexusLabs.Needlr.Generators;

[assembly: GenerateTypeRegistry(IncludeNamespacePrefixes = new[] { ""TestApp"" })]

namespace TestApp
{
    public interface IService { }
    internal class InternalService : IService { }
}";

        var attributeSource = GetAttributeSource();

        var compilation = CSharpCompilation.Create(
            "TestApp",
            [
                CSharpSyntaxTree.ParseText(attributeSource),
                CSharpSyntaxTree.ParseText(source)
            ],
            Basic.Reference.Assemblies.Net100.References.All,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        // Run the generator
        var generator = new TypeRegistryGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);

        driver = (CSharpGeneratorDriver)driver.RunGeneratorsAndUpdateCompilation(
            compilation,
            out _,
            out var diagnostics);

        // Assert - should NOT have NDLRGEN001 error for internal types in current assembly
        var ndlrgen001Diagnostics = diagnostics
            .Where(d => d.Id == "NDLRGEN001")
            .ToList();

        Assert.Empty(ndlrgen001Diagnostics);
    }

    [Fact]
    public void Generator_DoesNotEmitDiagnostic_WhenNamespaceDoesNotMatch()
    {
        // Create a "referenced" assembly with an internal type in a different namespace
        var referencedSource = @"
namespace OtherLib
{
    public interface IService { }
    internal class InternalService : IService { }
}";

        var referencedCompilation = CSharpCompilation.Create(
            "OtherLib",
            [CSharpSyntaxTree.ParseText(referencedSource)],
            Basic.Reference.Assemblies.Net100.References.All,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        // Create the "host" assembly that references it but scans a different namespace
        var hostSource = @"
using NexusLabs.Needlr.Generators;

[assembly: GenerateTypeRegistry(IncludeNamespacePrefixes = new[] { ""HostApp"" })]

namespace HostApp
{
    public class HostService { }
}";

        var attributeSource = GetAttributeSource();

        var references = Basic.Reference.Assemblies.Net100.References.All
            .Cast<MetadataReference>()
            .Append(referencedCompilation.ToMetadataReference())
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

        // Assert - should NOT have NDLRGEN001 because namespace doesn't match
        var ndlrgen001Diagnostics = diagnostics
            .Where(d => d.Id == "NDLRGEN001")
            .ToList();

        Assert.Empty(ndlrgen001Diagnostics);
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
