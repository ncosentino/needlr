using Xunit;

namespace NexusLabs.Needlr.Generators.Tests;

/// <summary>
/// Coverage for combining <c>[GenerateFactory]</c> with a generated constructor
/// (<c>[GenerateConstructor]</c> or a positive field-level constructor guard trigger).
/// <see cref="FactoryDiscoveryHelper.GetFactoryConstructors"/> must derive the factory's
/// constructor shape from the same field-derived model
/// <see cref="ConstructorGenerationDiscoveryHelper"/> uses for source emission, and the
/// generated factory call site must bind by parameter name so it stays correct even
/// when runtime and injectable fields are declared in an order that does not match the
/// factory's injectable-then-runtime parameter grouping.
/// </summary>
public sealed class GeneratedConstructorFactoryInteropTests
{
    [Fact]
    public void GenerateConstructor_WithGenerateFactory_PartitionsInjectableAndRuntimeFields()
    {
        var source = """
            using NexusLabs.Needlr.Generators;

            [assembly: GenerateTypeRegistry]

            namespace TestApp
            {
                public interface IRepository { }
                public class Repository : IRepository { }

                [GenerateFactory]
                [GenerateConstructor]
                public partial class ReportBuilder
                {
                    private readonly IRepository _repository;
                    private readonly string _templateName;
                }
            }
            """;

        var generatedCode = RunGenerator(source);

        Assert.Contains("IReportBuilderFactory", generatedCode);
        Assert.Contains("Create(string templateName)", generatedCode);
    }

    [Fact]
    public void GenerateConstructor_WithGenerateFactory_InterleavedFieldOrder_FactoryCallBindsArgumentsByName()
    {
        // The runtime (string) field is declared BEFORE the injectable field, so the
        // generated constructor's parameter order is (templateName, repository) -- the
        // opposite of the factory's injectable-then-runtime argument grouping. A
        // positional constructor call would bind these to the wrong parameters (and
        // fail to compile, since the types don't match); named arguments must be used
        // so the call binds correctly regardless of declaration order.
        var source = """
            using NexusLabs.Needlr.Generators;

            [assembly: GenerateTypeRegistry]

            namespace TestApp
            {
                public interface IRepository { }
                public class Repository : IRepository { }

                [GenerateFactory]
                [GenerateConstructor]
                public partial class ReportBuilder
                {
                    private readonly string _templateName;
                    private readonly IRepository _repository;
                }
            }
            """;

        var generatedCode = RunGenerator(source);

        Assert.Contains("templateName: templateName", generatedCode);
        Assert.Contains("repository: sp.GetRequiredService<global::TestApp.IRepository>()", generatedCode);
    }

    [Fact]
    public void GenerateConstructor_WithGenerateFactory_TypeIsNotDirectlyRegistered()
    {
        var source = """
            using NexusLabs.Needlr.Generators;

            [assembly: GenerateTypeRegistry]

            namespace TestApp
            {
                public interface IRepository { }
                public class Repository : IRepository { }

                [GenerateFactory]
                [GenerateConstructor]
                public partial class ReportBuilder
                {
                    private readonly IRepository _repository;
                    private readonly string _templateName;
                }
            }
            """;

        var generatedCode = RunGenerator(source);

        Assert.DoesNotContain("new(typeof(global::TestApp.ReportBuilder)", generatedCode);
    }

    [Fact]
    public void FieldTriggeredGeneration_WithGenerateFactory_GeneratesFactoryFromFieldDerivedConstructor()
    {
        var source = """
            using NexusLabs.Needlr.Generators;

            [assembly: GenerateTypeRegistry]

            namespace TestApp
            {
                public interface IRepository { }
                public class Repository : IRepository { }

                [GenerateFactory]
                public partial class TenantReportBuilder
                {
                    private readonly IRepository _repository;

                    [ConstructorGuard(ConstructorGuardKind.NotNullOrWhiteSpace)]
                    private readonly string _tenantName;
                }
            }
            """;

        var generatedCode = RunGenerator(source);

        Assert.Contains("ITenantReportBuilderFactory", generatedCode);
        Assert.Contains("Create(string tenantName)", generatedCode);
        Assert.Contains("tenantName: tenantName", generatedCode);
        Assert.Contains("repository: sp.GetRequiredService<global::TestApp.IRepository>()", generatedCode);
    }

    [Fact]
    public void GenerateConstructor_WithGenerateFactory_AllInjectableFields_SkipsFactoryAndRegistersDirectly()
    {
        // Matches the hand-written-constructor rule in FactoryDiscoveryHelper: a
        // constructor with no runtime parameters produces no factory value, so the
        // type must fall through to normal injectable registration instead.
        var source = """
            using NexusLabs.Needlr.Generators;

            [assembly: GenerateTypeRegistry]

            namespace TestApp
            {
                public interface IRepository { }
                public class Repository : IRepository { }
                public interface ILogger { }
                public class Logger : ILogger { }

                [GenerateFactory]
                [GenerateConstructor]
                public partial class AllInjectableService
                {
                    private readonly IRepository _repository;
                    private readonly ILogger _logger;
                }
            }
            """;

        var generatedCode = RunGenerator(source);

        Assert.DoesNotContain("IAllInjectableServiceFactory", generatedCode);
        Assert.Contains("sp => new global::TestApp.AllInjectableService(sp.GetRequiredService<global::TestApp.IRepository>(), sp.GetRequiredService<global::TestApp.ILogger>())", generatedCode);
    }

    private static string RunGenerator(string source)
    {
        return GeneratorTestRunner.ForFactory()
            .WithReference<GenerateConstructorAttribute>()
            .WithSource(source)
            .RunTypeRegistryGenerator();
    }
}
