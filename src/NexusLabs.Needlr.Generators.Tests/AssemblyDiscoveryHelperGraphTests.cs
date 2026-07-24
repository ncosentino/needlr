using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

using NexusLabs.Needlr.Generators.Models;

using Xunit;

namespace NexusLabs.Needlr.Generators.Tests;

/// <summary>
/// Tests cross-assembly graph discovery and source-location ownership.
/// </summary>
public sealed class AssemblyDiscoveryHelperGraphTests
{
    [Fact]
    public void CompilationReference_UsesProducerSymbolLocations()
    {
        var featureCompilation = CreateFeatureCompilation();
        var hostCompilation = CreateHostCompilation(
            featureCompilation.ToMetadataReference());

        var discovered =
            AssemblyDiscoveryHelper.DiscoverReferencedAssemblyTypesForGraph(
                hostCompilation);

        var featureTypes = Assert.Single(discovered).Value;
        var service = Assert.Single(
            featureTypes,
            type => type.TypeName == "global::Feature.FeatureService");
        Assert.Equal("Services/FeatureService.cs", service.SourceFilePath);
        Assert.Equal(3, service.SourceLine);

        var interfaceInfo = Assert.Single(service.InterfaceInfos);
        Assert.Equal(
            "global::Feature.IFeatureService",
            interfaceInfo.FullName);
        Assert.Equal(
            "Contracts/IFeatureService.cs",
            interfaceInfo.SourceFilePath);
        Assert.Equal(3, interfaceInfo.SourceLine);
    }

    [Fact]
    public void PortableExecutableReference_PreservesTypesWithUnknownLocations()
    {
        var featureCompilation = CreateFeatureCompilation();
        using var assemblyStream = new MemoryStream();
        var emitResult = featureCompilation.Emit(
            assemblyStream,
            cancellationToken: TestContext.Current.CancellationToken);
        Assert.True(
            emitResult.Success,
            $"Expected feature assembly to compile: {string.Join("; ", emitResult.Diagnostics)}");

        var hostCompilation = CreateHostCompilation(
            MetadataReference.CreateFromImage(assemblyStream.ToArray()));

        var discovered =
            AssemblyDiscoveryHelper.DiscoverReferencedAssemblyTypesForGraph(
                hostCompilation);

        var featureTypes = Assert.Single(discovered).Value;
        var service = Assert.Single(
            featureTypes,
            type => type.TypeName == "global::Feature.FeatureService");
        Assert.Null(service.SourceFilePath);
        Assert.Equal(0, service.SourceLine);

        var interfaceInfo = Assert.Single(service.InterfaceInfos);
        Assert.Equal(
            "global::Feature.IFeatureService",
            interfaceInfo.FullName);
        Assert.False(
            interfaceInfo.HasLocation,
            "Expected PE metadata to preserve the interface without fabricating a source location");
    }

    private static CSharpCompilation CreateFeatureCompilation()
    {
        var parseOptions = new CSharpParseOptions();
        var assemblyTree = CSharpSyntaxTree.ParseText(
            """
            [assembly: NexusLabs.Needlr.Generators.GenerateTypeRegistry]
            """,
            parseOptions,
            "Feature/AssemblyInfo.cs",
            cancellationToken: TestContext.Current.CancellationToken);
        var interfaceTree = CSharpSyntaxTree.ParseText(
            """
            namespace Feature;

            public interface IFeatureService
            {
            }
            """,
            parseOptions,
            "Contracts/IFeatureService.cs",
            cancellationToken: TestContext.Current.CancellationToken);
        var serviceTree = CSharpSyntaxTree.ParseText(
            """
            namespace Feature
            {
                public sealed class FeatureService : IFeatureService
                {
                }
            }

            namespace Feature.Generated
            {
                [global::NexusLabs.Needlr.DoNotAutoRegister]
                public sealed class ServiceCatalog
                {
                    public object[] Services { get; } = new object[]
                    {
                        new global::NexusLabs.Needlr.Catalog.InterfaceEntry(
                            "global::Feature.IFeatureService",
                            "Catalog/IncorrectInterfacePath.cs",
                            999),
                    };
                }
            }
            """,
            parseOptions,
            "Services/FeatureService.cs",
            cancellationToken: TestContext.Current.CancellationToken);

        return CSharpCompilation.Create(
            "Feature",
            [assemblyTree, interfaceTree, serviceTree],
            GetBaseReferences(),
            new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary));
    }

    private static CSharpCompilation CreateHostCompilation(
        MetadataReference featureReference)
    {
        var hostTree = CSharpSyntaxTree.ParseText(
            "namespace Host; public sealed class HostMarker;",
            cancellationToken: TestContext.Current.CancellationToken);
        return CSharpCompilation.Create(
            "Host",
            [hostTree],
            GetBaseReferences().Append(featureReference),
            new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary));
    }

    private static IEnumerable<MetadataReference> GetBaseReferences()
    {
        return Basic.Reference.Assemblies.Net100.References.All.Concat(
        [
            MetadataReference.CreateFromFile(
                typeof(GenerateTypeRegistryAttribute).Assembly.Location),
            MetadataReference.CreateFromFile(
                typeof(NexusLabs.Needlr.DoNotAutoRegisterAttribute)
                    .Assembly.Location),
        ]);
    }
}
