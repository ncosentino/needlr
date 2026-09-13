using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using NexusLabs.Needlr.Catalog;

using Xunit;

namespace NexusLabs.Needlr.Generators.Tests;

public sealed class RegistrationManifestCrossAssemblyTests
{
    [Fact]
    public void Manifest_IncludesPlainLibrariesAndReferencesOwnedRegistries()
    {
        var compilation = CreateRunner(
            """
            using NexusLabs.Needlr.Generators;

            [assembly: GenerateTypeRegistry(
                IncludeNamespacePrefixes = new[] { "Plain" },
                IncludeSelf = false)]

            namespace Host;

            public sealed record Marker;
            """)
            .WithCrossAssemblySource(
                "PlainLibrary",
                """
                namespace Plain;

                public sealed class PlainService
                {
                }
                """)
            .WithCrossAssemblySource(
                "OwnedLibrary",
                """
                [assembly: NexusLabs.Needlr.Generators.GenerateTypeRegistry(
                    IncludeNamespacePrefixes = new[] { "Owned" })]

                namespace Owned;

                public sealed class OwnedService
                {
                }
                """)
            .RunGeneratorCompilation(new TypeRegistryGenerator());

        using var manifest = RegistrationManifestTestHelper.ReadManifest(
            compilation.Assembly);
        var root = manifest.RootElement;

        var injectable = Assert.Single(
            root.GetProperty("injectableTypes").EnumerateArray());
        Assert.Equal(
            "global::Plain.PlainService",
            injectable.GetProperty("implementationType").GetString());
        Assert.Equal(
            "PlainLibrary",
            injectable.GetProperty("assemblyName").GetString());

        Assert.Equal(
            "OwnedLibrary",
            Assert.Single(
                root.GetProperty("referencedRegistryAssemblies")
                    .EnumerateArray())
                .GetString());
        Assert.DoesNotContain(
            "OwnedService",
            root.GetRawText(),
            StringComparison.Ordinal);
    }

    private static GeneratorTestRunner CreateRunner(string source)
    {
        return GeneratorTestRunner.ForTypeRegistry()
            .WithReference<IServiceCollection>()
            .WithReference<IConfiguration>()
            .WithReference<IServiceCatalog>()
            .WithRegistrationManifest()
            .WithSource(source);
    }
}
