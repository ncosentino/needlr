using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using NexusLabs.Needlr.Catalog;

using Xunit;

namespace NexusLabs.Needlr.Generators.Tests;

public sealed class RegistrationManifestGeneratorTests
{
    [Fact]
    public void Manifest_RepresentsGeneratedServicesAndFactoryExclusions()
    {
        var compilation = CreateRunner(
            """
            using NexusLabs.Needlr;
            using NexusLabs.Needlr.Generators;

            [assembly: GenerateTypeRegistry(
                IncludeNamespacePrefixes = new[] { "Example" })]

            namespace Example;

            public interface IClock
            {
            }

            public interface IExampleService
            {
            }

            [Transient]
            public sealed class ExampleService : IExampleService
            {
                public ExampleService(IClock clock)
                {
                }
            }

            [GenerateFactory]
            public sealed class FactoryCreatedService
            {
                public FactoryCreatedService(IClock clock, string name)
                {
                }
            }

            [DoNotAutoRegister]
            public sealed class ExcludedHelper
            {
            }

            [DoNotInject]
            public sealed class ExcludedDependency
            {
            }

            public sealed class RuntimeValue
            {
                public RuntimeValue(string value)
                {
                }
            }

            public sealed class OpenGeneric<T>
            {
            }

            public static class Outer
            {
                public sealed class Nested
                {
                }
            }

            public sealed record ExampleDto(string Value);
            """)
            .RunGeneratorCompilation(new TypeRegistryGenerator());

        using var manifest = RegistrationManifestTestHelper.ReadManifest(
            compilation.Assembly);
        var root = manifest.RootElement;

        Assert.Equal(
            new[]
            {
                "schemaVersion",
                "assemblyName",
                "isAotProject",
                "serviceCatalogRegistration",
                "referencedRegistryAssemblies",
                "injectableTypes",
                "decorators",
                "hostedServices",
                "interceptedServices",
                "factories",
                "providers",
                "options",
                "httpClients",
                "composedRegistrations",
                "plugins",
            },
            root.EnumerateObject()
                .Select(property => property.Name)
                .ToArray());
        Assert.Equal("1.0", root.GetProperty("schemaVersion").GetString());
        Assert.Equal("TestAssembly", root.GetProperty("assemblyName").GetString());

        var injectableTypes = root.GetProperty("injectableTypes");
        var service = Assert.Single(injectableTypes.EnumerateArray());
        Assert.Equal(
            "global::Example.ExampleService",
            service.GetProperty("implementationType").GetString());
        Assert.Equal("Transient", service.GetProperty("lifetime").GetString());
        Assert.Equal(
            new[]
            {
                "global::Example.ExampleService",
                "global::Example.IExampleService",
            },
            service.GetProperty("serviceTypes")
                .EnumerateArray()
                .Select(type => type.GetString()!)
                .ToArray());

        var factories = root.GetProperty("factories");
        var factory = Assert.Single(factories.EnumerateArray());
        Assert.Equal(
            "global::Example.FactoryCreatedService",
            factory.GetProperty("targetType").GetString());
        Assert.True(factory.GetProperty("generatesFunc").GetBoolean());
        Assert.True(factory.GetProperty("generatesInterface").GetBoolean());
        Assert.Equal(
            "global::TestAssembly.Generated.IFactoryCreatedServiceFactory",
            factory.GetProperty("generatedFactoryServiceType").GetString());
        var factoryConstructor = Assert.Single(
            factory.GetProperty("constructors").EnumerateArray());
        Assert.Equal(
            "global::Example.IClock",
            Assert.Single(
                factoryConstructor.GetProperty("injectableParameters")
                    .EnumerateArray())
                .GetProperty("type")
                .GetString());
        Assert.Equal(
            "string",
            Assert.Single(
                factoryConstructor.GetProperty("runtimeParameters")
                    .EnumerateArray())
                .GetProperty("type")
                .GetString());

        var json = root.GetRawText();
        Assert.DoesNotContain("ExcludedHelper", json);
        Assert.DoesNotContain("ExcludedDependency", json);
        Assert.DoesNotContain("RuntimeValue", json);
        Assert.DoesNotContain("OpenGeneric", json);
        Assert.DoesNotContain("Nested", json);
        Assert.DoesNotContain("ExampleDto", json);
    }

    [Fact]
    public void Manifest_ExplicitlyRepresentsAnEmptyRegistry()
    {
        var compilation = CreateRunner(
            """
            using NexusLabs.Needlr.Generators;

            [assembly: GenerateTypeRegistry(
                IncludeNamespacePrefixes = new[] { "Included" })]

            namespace Excluded;

            public sealed record Value(string Text);
            """)
            .RunGeneratorCompilation(new TypeRegistryGenerator());

        using var manifest = RegistrationManifestTestHelper.ReadManifest(
            compilation.Assembly);
        var root = manifest.RootElement;

        Assert.Equal("1.0", root.GetProperty("schemaVersion").GetString());
        Assert.Equal(
            System.Text.Json.JsonValueKind.Null,
            root.GetProperty("serviceCatalogRegistration").ValueKind);
        Assert.Empty(root.GetProperty("injectableTypes").EnumerateArray());
        Assert.Empty(root.GetProperty("plugins").EnumerateArray());
    }

    [Fact]
    public void Manifest_PreservesRegisterAsAndKeyedRegistrationSemantics()
    {
        var compilation = CreateRunner(
            """
            using NexusLabs.Needlr;
            using NexusLabs.Needlr.Generators;

            [assembly: GenerateTypeRegistry(
                IncludeNamespacePrefixes = new[] { "Example" })]

            namespace Example;

            public interface IVisibleService
            {
            }

            public interface IHiddenService
            {
            }

            [RegisterAs<IVisibleService>]
            [Keyed("primary\"line\n\u2028")]
            [Scoped]
            public sealed class KeyedService :
                IVisibleService,
                IHiddenService
            {
            }
            """)
            .RunGeneratorCompilation(new TypeRegistryGenerator());

        using var manifest = RegistrationManifestTestHelper.ReadManifest(
            compilation.Assembly);
        var service = Assert.Single(
            manifest.RootElement
                .GetProperty("injectableTypes")
                .EnumerateArray());

        Assert.Equal("Scoped", service.GetProperty("lifetime").GetString());
        Assert.Equal(
            new[]
            {
                "global::Example.KeyedService",
                "global::Example.IVisibleService",
            },
            service.GetProperty("serviceTypes")
                .EnumerateArray()
                .Select(type => type.GetString()!)
                .ToArray());
        Assert.Equal(
            "primary\"line\n\u2028",
            Assert.Single(
                service.GetProperty("serviceKeys").EnumerateArray())
                .GetString());
    }

    [Fact]
    public void Manifest_SeparatesPluginsFromDependencyInjectionRegistrations()
    {
        var compilation = CreateRunner(
            """
            using NexusLabs.Needlr;
            using NexusLabs.Needlr.Generators;

            [assembly: GenerateTypeRegistry(
                IncludeNamespacePrefixes = new[] { "Example" })]

            namespace Example;

            public sealed class ExamplePlugin : IServiceCollectionPlugin
            {
                public void Configure(ServiceCollectionPluginOptions options)
                {
                }
            }
            """)
            .RunGeneratorCompilation(new TypeRegistryGenerator());

        using var manifest = RegistrationManifestTestHelper.ReadManifest(
            compilation.Assembly);
        var root = manifest.RootElement;

        var plugin = Assert.Single(root.GetProperty("plugins").EnumerateArray());
        Assert.Equal(
            "global::Example.ExamplePlugin",
            plugin.GetProperty("pluginType").GetString());
        Assert.Equal(
            "global::NexusLabs.Needlr.IServiceCollectionPlugin",
            Assert.Single(
                plugin.GetProperty("pluginInterfaces").EnumerateArray())
                .GetString());
        Assert.Empty(root.GetProperty("injectableTypes").EnumerateArray());
    }

    [Fact]
    public void Manifest_IsReadableFromPortableExecutableMetadata()
    {
        var producerCompilation = CreateRunner(
            """
            using NexusLabs.Needlr.Generators;

            [assembly: GenerateTypeRegistry(
                IncludeNamespacePrefixes = new[] { "Producer" })]

            namespace Producer;

            public sealed class ExampleService
            {
            }
            """)
            .WithAssemblyName("Producer")
            .RunGeneratorCompilation(new TypeRegistryGenerator());

        var producerAssembly =
            RegistrationManifestTestHelper.EmitAndReference(producerCompilation);

        using var manifest =
            RegistrationManifestTestHelper.ReadManifest(producerAssembly);
        var service = Assert.Single(
            manifest.RootElement
                .GetProperty("injectableTypes")
                .EnumerateArray());

        Assert.Equal(
            "global::Producer.ExampleService",
            service.GetProperty("implementationType").GetString());
    }

    private static GeneratorTestRunner CreateRunner(string source)
    {
        return GeneratorTestRunner.ForTypeRegistry()
            .WithReference<IServiceCollection>()
            .WithReference<IConfiguration>()
            .WithReference<IServiceCatalog>()
            .WithSource(source);
    }
}
