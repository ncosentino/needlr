using Xunit;

namespace NexusLabs.Needlr.Generators.Tests.Provider;

public sealed class ProviderGeneratorTests
{
    [Fact]
    public void Generator_WithProviderOnInterface_GeneratesImplementationClass()
    {
        var source = """
            using NexusLabs.Needlr;
            using NexusLabs.Needlr.Generators;

            [assembly: GenerateTypeRegistry]

            namespace TestApp
            {
                public interface IOrderRepository { }
                public class OrderRepository : IOrderRepository { }

                [Provider]
                public interface IOrderServicesProvider
                {
                    IOrderRepository Repository { get; }
                }
            }
            """;

        var generatedCode = RunGenerator(source);

        Assert.Contains("public sealed class OrderServicesProvider", generatedCode);
        Assert.Contains(": IOrderServicesProvider", generatedCode);
    }

    [Fact]
    public void Generator_WithProviderOnInterface_GeneratesPropertyFromServiceProvider()
    {
        var source = """
            using NexusLabs.Needlr;
            using NexusLabs.Needlr.Generators;

            [assembly: GenerateTypeRegistry]

            namespace TestApp
            {
                public interface IOrderRepository { }
                public class OrderRepository : IOrderRepository { }

                [Provider]
                public interface IOrderServicesProvider
                {
                    IOrderRepository Repository { get; }
                }
            }
            """;

        var generatedCode = RunGenerator(source);

        // Should use constructor injection
        Assert.Contains("public OrderServicesProvider(global::TestApp.IOrderRepository repository)", generatedCode);
        Assert.Contains("public global::TestApp.IOrderRepository Repository { get; }", generatedCode);
    }

    [Fact]
    public void Generator_WithProviderOnInterface_RegistersAsSingleton()
    {
        var source = """
            using NexusLabs.Needlr;
            using NexusLabs.Needlr.Generators;

            [assembly: GenerateTypeRegistry]

            namespace TestApp
            {
                public interface IOrderRepository { }
                public class OrderRepository : IOrderRepository { }

                [Provider]
                public interface IOrderServicesProvider
                {
                    IOrderRepository Repository { get; }
                }
            }
            """;

        var generatedCode = RunGenerator(source);

        // Provider should be registered as singleton
        Assert.Contains("AddSingleton<global::TestApp.IOrderServicesProvider", generatedCode);
    }

    [Fact]
    public void Generator_WithProviderOnInterface_MultipleProperties()
    {
        var source = """
            using NexusLabs.Needlr;
            using NexusLabs.Needlr.Generators;

            [assembly: GenerateTypeRegistry]

            namespace TestApp
            {
                public interface IOrderRepository { }
                public class OrderRepository : IOrderRepository { }

                public interface IOrderValidator { }
                public class OrderValidator : IOrderValidator { }

                [Provider]
                public interface IOrderServicesProvider
                {
                    IOrderRepository Repository { get; }
                    IOrderValidator Validator { get; }
                }
            }
            """;

        var generatedCode = RunGenerator(source);

        Assert.Contains("IOrderRepository repository", generatedCode);
        Assert.Contains("IOrderValidator validator", generatedCode);
        Assert.Contains("public global::TestApp.IOrderRepository Repository { get; }", generatedCode);
        Assert.Contains("public global::TestApp.IOrderValidator Validator { get; }", generatedCode);
    }

    [Fact(Skip = "NDLRGEN031 diagnostic not yet implemented - Phase 5")]
    public void Generator_WithProviderOnClass_RequiresPartial()
    {
        var source = """
            using NexusLabs.Needlr;
            using NexusLabs.Needlr.Generators;

            [assembly: GenerateTypeRegistry]

            namespace TestApp
            {
                public interface IOrderRepository { }
                public class OrderRepository : IOrderRepository { }

                [Provider(typeof(IOrderRepository))]
                public class OrderProvider { }
            }
            """;

        var diagnostics = RunGeneratorDiagnostics(source);

        // Should emit NDLRGEN031 for non-partial class
        Assert.Contains(diagnostics, d => d.Id == "NDLRGEN031");
    }

    [Fact]
    public void Generator_WithProviderOnPartialClass_GeneratesInterfaceAndImplementation()
    {
        var source = """
            using NexusLabs.Needlr;
            using NexusLabs.Needlr.Generators;

            [assembly: GenerateTypeRegistry]

            namespace TestApp
            {
                public interface IOrderRepository { }
                public class OrderRepository : IOrderRepository { }

                [Provider(typeof(IOrderRepository))]
                public partial class OrderProvider { }
            }
            """;

        var generatedCode = RunGenerator(source);

        // Should generate interface
        Assert.Contains("public interface IOrderProvider", generatedCode);
        // Should generate partial class that implements interface
        Assert.Contains("public partial class OrderProvider : IOrderProvider", generatedCode);
        // Should have property derived from type name
        Assert.Contains("OrderRepository", generatedCode);
    }

    [Fact]
    public void Generator_WithProviderShorthand_SupportsMultipleTypes()
    {
        var source = """
            using NexusLabs.Needlr;
            using NexusLabs.Needlr.Generators;

            [assembly: GenerateTypeRegistry]

            namespace TestApp
            {
                public interface IOrderRepository { }
                public class OrderRepository : IOrderRepository { }

                public interface IOrderValidator { }
                public class OrderValidator : IOrderValidator { }

                [Provider(typeof(IOrderRepository), typeof(IOrderValidator))]
                public partial class OrderDependenciesProvider { }
            }
            """;

        var generatedCode = RunGenerator(source);

        Assert.Contains("public interface IOrderDependenciesProvider", generatedCode);
        Assert.Contains("IOrderRepository OrderRepository { get; }", generatedCode);
        Assert.Contains("IOrderValidator OrderValidator { get; }", generatedCode);
    }

    [Fact]
    public void Generator_WithProviderOptional_GeneratesNullableProperty()
    {
        var source = """
            using NexusLabs.Needlr;
            using NexusLabs.Needlr.Generators;

            [assembly: GenerateTypeRegistry]

            namespace TestApp
            {
                public interface ILogger { }

                [Provider(Optional = new[] { typeof(ILogger) })]
                public partial class ServicesProvider { }
            }
            """;

        var generatedCode = RunGenerator(source);

        // Optional should use GetService<T> and be nullable
        Assert.Contains("ILogger?", generatedCode);
    }

    [Fact]
    public void Generator_ProviderImplementation_HasXmlDocComments()
    {
        var source = """
            using NexusLabs.Needlr;
            using NexusLabs.Needlr.Generators;

            [assembly: GenerateTypeRegistry]

            namespace TestApp
            {
                public interface IOrderRepository { }
                public class OrderRepository : IOrderRepository { }

                [Provider]
                public interface IOrderServicesProvider
                {
                    IOrderRepository Repository { get; }
                }
            }
            """;

        var generatedCode = RunGeneratorWithDocs(source);

        // Provider should have singleton documentation
        Assert.Contains("Singleton", generatedCode);
    }

    [Fact]
    public void Generator_ProviderNaming_UsesProviderSuffix()
    {
        var source = """
            using NexusLabs.Needlr;
            using NexusLabs.Needlr.Generators;

            [assembly: GenerateTypeRegistry]

            namespace TestApp
            {
                public interface IOrderRepository { }
                public class OrderRepository : IOrderRepository { }

                [Provider]
                public interface IOrderServicesProvider
                {
                    IOrderRepository Repository { get; }
                }
            }
            """;

        var generatedCode = RunGenerator(source);

        // Generated class should maintain Provider naming
        Assert.Contains("OrderServicesProvider", generatedCode);
    }

    private static string RunGenerator(string source)
    {
        return GeneratorTestRunner.ForProvider()
            .WithSource(source)
            .RunTypeRegistryGenerator();
    }

    private static string RunGeneratorWithDocs(string source)
    {
        return GeneratorTestRunner.ForProvider()
            .WithSource(source)
            .WithDocumentationMode()
            .RunTypeRegistryGenerator();
    }

    private static System.Collections.Generic.IReadOnlyList<Microsoft.CodeAnalysis.Diagnostic> RunGeneratorDiagnostics(string source)
    {
        return GeneratorTestRunner.ForProvider()
            .WithSource(source)
            .RunTypeRegistryGeneratorDiagnostics();
    }
}
