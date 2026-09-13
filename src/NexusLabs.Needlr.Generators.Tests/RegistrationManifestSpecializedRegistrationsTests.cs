using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using NexusLabs.Needlr.Catalog;

using Xunit;

namespace NexusLabs.Needlr.Generators.Tests;

public sealed class RegistrationManifestSpecializedRegistrationsTests
{
    [Fact]
    public void Manifest_RepresentsDecoratorHostedAndInterceptorRegistrations()
    {
        var compilation = CreateRunner(
            """
            using System.Threading;
            using System.Threading.Tasks;
            using NexusLabs.Needlr;
            using NexusLabs.Needlr.Generators;

            [assembly: GenerateTypeRegistry(
                IncludeNamespacePrefixes = new[] { "Example" })]

            namespace Microsoft.Extensions.Hosting
            {
                public interface IHostedService
                {
                }

                public abstract class BackgroundService : IHostedService
                {
                    protected abstract Task ExecuteAsync(
                        CancellationToken stoppingToken);
                }
            }

            namespace Example
            {
                public interface IService
                {
                    void Execute();
                }

                public sealed class Service : IService
                {
                    public void Execute()
                    {
                    }
                }

                [DecoratorFor<IService>(Order = -10)]
                public sealed class ServiceDecorator : IService
                {
                    public ServiceDecorator(IService inner)
                    {
                    }

                    public void Execute()
                    {
                    }
                }

                public sealed class AuditInterceptor : IMethodInterceptor
                {
                    public ValueTask<object?> InterceptAsync(
                        IMethodInvocation invocation) =>
                        invocation.ProceedAsync();
                }

                [Intercept<AuditInterceptor>]
                public sealed class InterceptedService : IService
                {
                    public void Execute()
                    {
                    }
                }

                public sealed class Worker :
                    Microsoft.Extensions.Hosting.BackgroundService
                {
                    protected override Task ExecuteAsync(
                        CancellationToken stoppingToken) =>
                        Task.CompletedTask;
                }
            }
            """)
            .RunGeneratorCompilation(new TypeRegistryGenerator());

        using var manifest = RegistrationManifestTestHelper.ReadManifest(
            compilation.Assembly);
        var root = manifest.RootElement;

        var decorator = Assert.Single(
            root.GetProperty("decorators").EnumerateArray());
        Assert.Equal(
            "global::Example.IService",
            decorator.GetProperty("serviceType").GetString());
        Assert.Equal(
            "global::Example.ServiceDecorator",
            decorator.GetProperty("decoratorType").GetString());
        Assert.Equal(-10, decorator.GetProperty("order").GetInt32());

        var intercepted = Assert.Single(
            root.GetProperty("interceptedServices").EnumerateArray());
        Assert.Equal(
            "global::Example.InterceptedService",
            intercepted.GetProperty("implementationType").GetString());
        Assert.Equal(
            "global::TestAssembly.Generated.InterceptedService_InterceptorProxy",
            intercepted.GetProperty("proxyType").GetString());
        Assert.Equal(
            "global::Example.AuditInterceptor",
            Assert.Single(
                intercepted.GetProperty("interceptorTypes").EnumerateArray())
                .GetString());

        var hosted = Assert.Single(
            root.GetProperty("hostedServices").EnumerateArray());
        Assert.Equal(
            "global::Example.Worker",
            hosted.GetProperty("implementationType").GetString());
        Assert.Equal("Singleton", hosted.GetProperty("lifetime").GetString());
    }

    [Fact]
    public void Manifest_RepresentsProviderOptionsAndHttpClientRegistrations()
    {
        var compilation = CreateRunner(
            """
            using System;
            using System.ComponentModel.DataAnnotations;
            using NexusLabs.Needlr.Generators;

            [assembly: GenerateTypeRegistry(
                IncludeNamespacePrefixes = new[] { "Example" })]

            namespace Example;

            public interface IService
            {
            }

            public sealed class Service : IService
            {
            }

            [Provider]
            public interface IServiceProviderView
            {
                IService Service { get; }
            }

            [Provider(typeof(IService))]
            public sealed class InvalidProvider
            {
            }

            [Options("Settings", Name = "Primary", ValidateOnStart = true)]
            public sealed class SettingsOptions
            {
                [Required]
                public string Value { get; set; } = string.Empty;
            }

            [HttpClientOptions("Clients:Api", Name = "api")]
            public sealed record ApiHttpClientOptions :
                INamedHttpClientOptions,
                IHttpClientTimeout
            {
                public TimeSpan Timeout { get; init; }
            }
            """)
            .RunGeneratorCompilation(new TypeRegistryGenerator());

        using var manifest = RegistrationManifestTestHelper.ReadManifest(
            compilation.Assembly);
        var root = manifest.RootElement;

        var provider = Assert.Single(
            root.GetProperty("providers").EnumerateArray());
        Assert.Equal("interface", provider.GetProperty("mode").GetString());
        Assert.Equal(
            "global::Example.IServiceProviderView",
            provider.GetProperty("serviceType").GetString());
        Assert.Equal(
            "global::TestAssembly.Generated.ServiceProviderView",
            provider.GetProperty("implementationType").GetString());
        Assert.DoesNotContain(
            "InvalidProvider",
            root.GetRawText(),
            StringComparison.Ordinal);

        var options = Assert.Single(
            root.GetProperty("options").EnumerateArray());
        Assert.Equal(
            "global::Example.SettingsOptions",
            options.GetProperty("optionsType").GetString());
        Assert.Equal(
            "reflection",
            options.GetProperty("bindingMode").GetString());
        Assert.Equal("Settings", options.GetProperty("sectionName").GetString());
        Assert.Equal("Primary", options.GetProperty("name").GetString());
        Assert.True(options.GetProperty("validateOnStart").GetBoolean());
        Assert.Equal(
            "global::TestAssembly.Generated.SettingsOptionsDataAnnotationsValidator",
            options.GetProperty("generatedDataAnnotationsValidatorType")
                .GetString());

        var httpClient = Assert.Single(
            root.GetProperty("httpClients").EnumerateArray());
        Assert.Equal(
            "global::Example.ApiHttpClientOptions",
            httpClient.GetProperty("optionsType").GetString());
        Assert.Equal("api", httpClient.GetProperty("clientName").GetString());
        Assert.Equal(
            "Clients:Api",
            httpClient.GetProperty("sectionName").GetString());
        Assert.Equal(
            "Timeout",
            Assert.Single(
                httpClient.GetProperty("capabilities").EnumerateArray())
                .GetString());
    }

    [Fact]
    public void Manifest_RepresentsComposedRegistrations()
    {
        var compilation = CreateRunner(
            """
            using NexusLabs.Needlr.Generators;

            [assembly: GenerateTypeRegistry(
                IncludeNamespacePrefixes = new[] { "Example" })]

            namespace Example;

            public interface IDefinition<T>
                where T : class
            {
            }

            public interface IFacade
            {
            }

            public sealed class Data
            {
            }

            public sealed class Definition : IDefinition<Data>
            {
            }

            [RegisterClosedOverImplementationsOf(
                typeof(IDefinition<>),
                As = typeof(IFacade),
                Lifetime = InjectableLifetime.Scoped)]
            public sealed class Composition<T> : IFacade
                where T : class
            {
                public Composition(IDefinition<T> definition)
                {
                }
            }
            """)
            .RunGeneratorCompilation(new TypeRegistryGenerator());

        using var manifest = RegistrationManifestTestHelper.ReadManifest(
            compilation.Assembly);
        var registration = Assert.Single(
            manifest.RootElement
                .GetProperty("composedRegistrations")
                .EnumerateArray());

        Assert.Equal(
            "global::Example.IFacade",
            registration.GetProperty("serviceType").GetString());
        Assert.Equal(
            "global::Example.Composition<global::Example.Data>",
            registration.GetProperty("implementationType").GetString());
        Assert.Equal("Scoped", registration.GetProperty("lifetime").GetString());
        Assert.Equal(
            "sp.GetRequiredService<global::Example.IDefinition<global::Example.Data>>()",
            Assert.Single(
                registration.GetProperty("constructorArguments")
                    .EnumerateArray())
                .GetString());
    }

    [Fact]
    public void Manifest_RecordsAotOptionsBindingMode()
    {
        var compilation = CreateRunner(
            """
            using NexusLabs.Needlr.Generators;

            [assembly: GenerateTypeRegistry(
                IncludeNamespacePrefixes = new[] { "Example" })]

            namespace Example;

            [Options("Settings")]
            public sealed class SettingsOptions
            {
                public string Value { get; set; } = string.Empty;
            }
            """)
            .WithAotMode()
            .RunGeneratorCompilation(new TypeRegistryGenerator());

        using var manifest = RegistrationManifestTestHelper.ReadManifest(
            compilation.Assembly);
        var options = Assert.Single(
            manifest.RootElement.GetProperty("options").EnumerateArray());

        Assert.True(
            manifest.RootElement.GetProperty("isAotProject").GetBoolean());
        Assert.Equal(
            "aot-configure",
            options.GetProperty("bindingMode").GetString());
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
