using System.Linq;

using Xunit;

namespace NexusLabs.Needlr.Generators.Tests;

/// <summary>
/// Regression coverage proving <see cref="TypeRegistryGenerator"/> uses the same
/// field-derived constructor model as <see cref="GeneratedConstructorGenerator"/>
/// for types using <c>[GenerateConstructor]</c> or a positive field-level guard
/// trigger, instead of emitting an incorrect parameterless factory.
/// </summary>
public sealed class GeneratedConstructorTypeRegistryIntegrationTests
{
    [Fact]
    public void GenerateConstructor_InjectableType_FactoryResolvesDependencyInsteadOfParameterlessNew()
    {
        var source = """
            using NexusLabs.Needlr.Generators;

            [assembly: GenerateTypeRegistry]

            namespace TestApp
            {
                public interface IRepository { }
                public class Repository : IRepository { }

                [GenerateConstructor]
                public partial class UserService
                {
                    private readonly IRepository _repository;
                }
            }
            """;

        var generatedCode = GeneratorTestRunner.ForTypeRegistry()
            .WithReference<GenerateConstructorAttribute>()
            .WithSource(source)
            .RunTypeRegistryGenerator();

        Assert.Contains("sp => new global::TestApp.UserService(sp.GetRequiredService<global::TestApp.IRepository>())", generatedCode);
        Assert.DoesNotContain("new global::TestApp.UserService()", generatedCode);
    }

    [Fact]
    public void FieldTriggeredGeneration_InjectableType_FactoryResolvesAllEligibleFields()
    {
        var source = """
            using NexusLabs.Needlr.Generators;

            [assembly: GenerateTypeRegistry]

            namespace TestApp
            {
                public interface IRepository { }
                public class Repository : IRepository { }

                public interface ILogger { }
                public class Logger : ILogger { }

                public partial class TenantService
                {
                    private readonly IRepository _repository;

                    [ConstructorGuard(typeof(NonNullGuard))]
                    private readonly ILogger _logger;
                }

                public static class NonNullGuard
                {
                    public static void Validate(ILogger value, string parameterName) { }
                }
            }
            """;

        var generatedCode = GeneratorTestRunner.ForTypeRegistry()
            .WithReference<GenerateConstructorAttribute>()
            .WithSource(source)
            .RunTypeRegistryGenerator();

        Assert.Contains("sp => new global::TestApp.TenantService(sp.GetRequiredService<global::TestApp.IRepository>(), sp.GetRequiredService<global::TestApp.ILogger>())", generatedCode);
    }

    [Fact]
    public void FieldTriggeredGeneration_WithNonInjectableParameter_IsExcludedFromAutomaticRegistration()
    {
        var source = """
            using NexusLabs.Needlr.Generators;

            [assembly: GenerateTypeRegistry]

            namespace TestApp
            {
                public interface IRepository { }
                public class Repository : IRepository { }

                public partial class TenantService
                {
                    private readonly IRepository _repository;

                    [ConstructorGuard(ConstructorGuardKind.NotNullOrWhiteSpace)]
                    private readonly string _tenantName;
                }
            }
            """;

        var generatedCode = GeneratorTestRunner.ForTypeRegistry()
            .WithReference<GenerateConstructorAttribute>()
            .WithSource(source)
            .RunTypeRegistryGenerator();

        Assert.DoesNotContain("global::TestApp.TenantService", generatedCode);
    }

    [Fact]
    public void GenerateConstructor_HostedService_ServiceCatalogReflectsGeneratedConstructorDependency()
    {
        var source = """
            using Microsoft.Extensions.Hosting;
            using NexusLabs.Needlr.Generators;

            [assembly: GenerateTypeRegistry(IncludeNamespacePrefixes = new[] { "TestApp" })]

            namespace TestApp
            {
                public interface IRepository { }

                [GenerateConstructor]
                public partial class MyWorker : BackgroundService
                {
                    private readonly IRepository _repository;

                    protected override System.Threading.Tasks.Task ExecuteAsync(System.Threading.CancellationToken stoppingToken)
                    {
                        return System.Threading.Tasks.Task.CompletedTask;
                    }
                }
            }
            """;

        var files = GeneratorTestRunner.ForHostedServiceWithInlineTypes()
            .WithReference<GenerateConstructorAttribute>()
            .WithSource(source)
            .RunTypeRegistryGeneratorFiles();

        var typeRegistry = files.First(f => f.FilePath.Contains("TypeRegistry.g.cs")).Content;
        Assert.Contains("services.AddSingleton<global::TestApp.MyWorker>();", typeRegistry);

        var serviceCatalog = files.First(f => f.FilePath.Contains("ServiceCatalog")).Content;
        Assert.Contains(
            "new global::NexusLabs.Needlr.Catalog.HostedServiceCatalogEntry(\"global::TestApp.MyWorker\", \"MyWorker\", \"TestAssembly\", new global::NexusLabs.Needlr.Catalog.ConstructorParameterEntry[] { new global::NexusLabs.Needlr.Catalog.ConstructorParameterEntry(\"repository\", \"global::TestApp.IRepository\", false, null), }",
            serviceCatalog);
    }

    [Fact]
    public void GenerateConstructor_WithExplicitLifetime_PreservesExplicitLifetime()
    {
        var source = """
            using NexusLabs.Needlr;
            using NexusLabs.Needlr.Generators;

            [assembly: GenerateTypeRegistry]

            namespace TestApp
            {
                public interface IRepository { }
                public class Repository : IRepository { }

                [Scoped]
                [GenerateConstructor]
                public partial class UserService
                {
                    private readonly IRepository _repository;
                }
            }
            """;

        var generatedCode = GeneratorTestRunner.ForTypeRegistry()
            .WithReference<GenerateConstructorAttribute>()
            .WithSource(source)
            .RunTypeRegistryGenerator();

        Assert.Contains("InjectableLifetime.Scoped", generatedCode);
        Assert.Contains("sp => new global::TestApp.UserService(sp.GetRequiredService<global::TestApp.IRepository>())", generatedCode);
    }

    [Fact]
    public void GenerateConstructor_ImplementingInterface_IsNotEmittedAsParameterlessPlugin()
    {
        // A [GenerateConstructor] class implementing a non-System interface must be
        // registered via its injectable factory (which resolves IRepository), and must
        // never also be emitted into `_plugins` with a `() => new GeneratedWorkItem()`
        // factory: once the sibling GeneratedConstructorGenerator pass emits the real
        // constructor, that parameterless factory would no longer compile.
        var source = """
            using NexusLabs.Needlr.Generators;

            [assembly: GenerateTypeRegistry]

            namespace TestApp
            {
                public interface IRepository { }
                public class Repository : IRepository { }

                public interface IWorkItem { }

                [GenerateConstructor]
                public partial class GeneratedWorkItem : IWorkItem
                {
                    private readonly IRepository _repository;
                }
            }
            """;

        var generatedCode = GeneratorTestRunner.ForTypeRegistry()
            .WithReference<GenerateConstructorAttribute>()
            .WithSource(source)
            .RunTypeRegistryGenerator();

        Assert.Contains("sp => new global::TestApp.GeneratedWorkItem(sp.GetRequiredService<global::TestApp.IRepository>())", generatedCode);

        var pluginsSection = ExtractPluginsSection(generatedCode);
        Assert.DoesNotContain("GeneratedWorkItem", pluginsSection);
    }

    [Fact]
    public void FieldTriggeredGeneration_HostedServiceWithNonInjectableParameter_SkipsAutomaticHostedRegistration()
    {
        // A hosted service whose generated constructor requires a non-container-resolvable
        // parameter (here, a plain guarded string) can never actually be activated by
        // `services.AddSingleton<T>()`. Needlr must skip automatic hosted registration
        // entirely rather than register a worker its own container can't construct.
        var source = """
            using Microsoft.Extensions.Hosting;
            using NexusLabs.Needlr.Generators;

            [assembly: GenerateTypeRegistry(IncludeNamespacePrefixes = new[] { "TestApp" })]

            namespace TestApp
            {
                public interface IRepository { }

                public partial class MyWorker : BackgroundService
                {
                    private readonly IRepository _repository;

                    [ConstructorGuard(ConstructorGuardKind.NotNullOrWhiteSpace)]
                    private readonly string _workerName;

                    protected override System.Threading.Tasks.Task ExecuteAsync(System.Threading.CancellationToken stoppingToken)
                    {
                        return System.Threading.Tasks.Task.CompletedTask;
                    }
                }
            }
            """;

        var files = GeneratorTestRunner.ForHostedServiceWithInlineTypes()
            .WithReference<GenerateConstructorAttribute>()
            .WithSource(source)
            .RunTypeRegistryGeneratorFiles();

        var typeRegistry = files.First(f => f.FilePath.Contains("TypeRegistry.g.cs")).Content;
        Assert.DoesNotContain("MyWorker", typeRegistry);
    }

    [Fact]
    public void GenerateConstructor_DecoratorPatternField_IsClassifiedSameAsHandWrittenDecorator()
    {
        // A [GenerateConstructor] class that implements IRepository and also takes an
        // IRepository as a generated-constructor field is a decorator: it must not be
        // auto-registered as IRepository (which would create a circular self-dependency),
        // matching the classification hand-written decorator constructors already get.
        var source = """
            using NexusLabs.Needlr.Generators;

            [assembly: GenerateTypeRegistry]

            namespace TestApp
            {
                public interface IRepository { }
                public class Repository : IRepository { }

                [GenerateConstructor]
                public partial class LoggingRepositoryDecorator : IRepository
                {
                    private readonly IRepository _inner;
                }
            }
            """;

        var generatedCode = GeneratorTestRunner.ForTypeRegistry()
            .WithReference<GenerateConstructorAttribute>()
            .WithSource(source)
            .RunTypeRegistryGenerator();

        // The decorator's own factory must still resolve its inner dependency, and its
        // registerable-interfaces list must be empty -- IRepository is excluded because
        // the type both implements it and takes it as a generated-constructor parameter,
        // exactly like Needlr already classifies hand-written decorators.
        Assert.Contains(
            "new(typeof(global::TestApp.LoggingRepositoryDecorator), Array.Empty<Type>(), InjectableLifetime.Singleton, sp => new global::TestApp.LoggingRepositoryDecorator(sp.GetRequiredService<global::TestApp.IRepository>()), Array.Empty<string>()),",
            generatedCode);
    }

    private static string ExtractPluginsSection(string generatedCode)
    {
        var startIndex = generatedCode.IndexOf("private static readonly PluginTypeInfo[]", System.StringComparison.Ordinal);
        if (startIndex < 0)
            return string.Empty;

        var endIndex = generatedCode.IndexOf("];", startIndex, System.StringComparison.Ordinal);
        if (endIndex < 0)
            return string.Empty;

        return generatedCode.Substring(startIndex, endIndex - startIndex + 2);
    }
}
