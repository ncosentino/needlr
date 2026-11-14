using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using NexusLabs.Needlr.Catalog;

using Xunit;

namespace NexusLabs.Needlr.Generators.Tests;

public sealed class FactoryConsumerGeneratorTests
{
    [Theory]
    [InlineData("IWidgetFactory", "using TestAssembly.Generated;", false)]
    [InlineData("global::TestAssembly.Generated.IWidgetFactory", "", true)]
    [InlineData("WidgetFactory", "using WidgetFactory = TestAssembly.Generated.IWidgetFactory;", true)]
    public void FactoryConsumer_RegistersWithBoundConstructorAndManifest(
        string parameterType,
        string import,
        bool aot)
    {
        var compilation = GeneratorTestRunner.ForFactory()
            .WithReference<IServiceCollection>()
            .WithReference<IConfiguration>()
            .WithReference<IServiceCatalog>()
            .WithRegistrationManifest()
            .WithAotMode(aot)
            .WithSource(
                $$"""
                using NexusLabs.Needlr.Generators;
                {{import}}

                [assembly: GenerateTypeRegistry]

                namespace Example.Nested;

                public interface IWidget { }

                [GenerateFactory<IWidget>(Mode = FactoryGenerationMode.Interface)]
                public sealed class Widget(int number) : IWidget { }

                public sealed class Consumer({{parameterType}} factory) { }
                """)
            .RunGeneratorCompilation(new TypeRegistryGenerator());

        Assert.Empty(compilation.GetDiagnostics(TestContext.Current.CancellationToken)
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));

        using var manifest = RegistrationManifestTestHelper.ReadManifest(compilation.Assembly);
        var consumer = Assert.Single(manifest.RootElement.GetProperty("injectableTypes").EnumerateArray());
        Assert.Equal("global::Example.Nested.Consumer", consumer.GetProperty("implementationType").GetString());
        var parameter = Assert.Single(consumer.GetProperty("constructorParameters").EnumerateArray());
        Assert.Equal(
            "global::TestAssembly.Generated.IWidgetFactory",
            parameter.GetProperty("type").GetString());

        var registry = Assert.Single(compilation.SyntaxTrees,
            tree => tree.FilePath.EndsWith("TypeRegistry.g.cs", StringComparison.Ordinal)).ToString();
        Assert.Contains(
            "new global::Example.Nested.Consumer(sp.GetRequiredService<global::TestAssembly.Generated.IWidgetFactory>())",
            registry);
    }

    [Theory]
    [InlineData("IMissingFactory")]
    [InlineData("global::Wrong.Generated.IWidgetFactory")]
    public void FactoryConsumer_UnrelatedUnresolvedParameter_RemainsExcluded(string parameterType)
    {
        var compilation = GeneratorTestRunner.ForFactory()
            .WithRegistrationManifest()
            .WithSource(
                $$"""
                using NexusLabs.Needlr.Generators;

                [assembly: GenerateTypeRegistry]

                namespace Example;

                [GenerateFactory]
                public sealed class Widget(int number) { }

                public sealed class Consumer({{parameterType}} factory) { }
                """)
            .RunGeneratorCompilation(new TypeRegistryGenerator());

        using var manifest = RegistrationManifestTestHelper.ReadManifest(compilation.Assembly);
        Assert.Empty(manifest.RootElement.GetProperty("injectableTypes").EnumerateArray());
        Assert.Contains(compilation.GetDiagnostics(TestContext.Current.CancellationToken),
            diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
    }

    [Theory]
    [InlineData("FactoryGenerationMode.Func", "int number", "")]
    [InlineData("FactoryGenerationMode.Interface", "", "")]
    [InlineData("FactoryGenerationMode.Interface", "int number", """ExcludeNamespacePrefixes = new[] { "Example.Products" }""")]
    public void FactoryConsumer_InterfaceNotEmitted_RemainsExcluded(
        string mode,
        string parameters,
        string registryOptions)
    {
        var compilation = GeneratorTestRunner.ForFactory()
            .WithRegistrationManifest()
            .WithSource(
                $$"""
                using NexusLabs.Needlr.Generators;
                using TestAssembly.Generated;

                [assembly: GenerateTypeRegistry({{registryOptions}})]

                namespace Example.Products
                {
                    [GenerateFactory(Mode = {{mode}})]
                    public sealed class Widget({{parameters}}) { }
                }

                namespace Example.Consumers
                {
                    public sealed class Consumer(IWidgetFactory factory) { }
                }
                """)
            .RunGeneratorCompilation(new TypeRegistryGenerator());

        using var manifest = RegistrationManifestTestHelper.ReadManifest(compilation.Assembly);
        Assert.DoesNotContain(manifest.RootElement.GetProperty("injectableTypes").EnumerateArray(),
            entry => entry.GetProperty("implementationType").GetString() == "global::Example.Consumers.Consumer");
    }
}
