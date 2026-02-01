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

    [Fact]
    public void Generator_WithOptionalProperty_GeneratesNullableType()
    {
        var source = """
            using NexusLabs.Needlr;
            using NexusLabs.Needlr.Generators;

            [assembly: GenerateTypeRegistry]

            namespace TestApp
            {
                public interface IOptionalService { }

                [Provider]
                public interface IServicesProvider
                {
                    IOptionalService? OptionalService { get; }
                }
            }
            """;

        var generatedCode = RunGenerator(source);

        // Optional properties should be nullable in constructor parameter
        Assert.Contains("IOptionalService? optionalService = null", generatedCode);
        // Optional properties should be nullable in property declaration
        Assert.Contains("IOptionalService? OptionalService { get; }", generatedCode);
    }

    [Fact]
    public void Generator_WithCollectionProperty_GeneratesIEnumerable()
    {
        var source = """
            using NexusLabs.Needlr;
            using NexusLabs.Needlr.Generators;
            using System.Collections.Generic;

            [assembly: GenerateTypeRegistry]

            namespace TestApp
            {
                public interface IHandler { }

                [Provider]
                public interface IServicesProvider
                {
                    IEnumerable<IHandler> Handlers { get; }
                }
            }
            """;

        var generatedCode = RunGenerator(source);

        // Collection properties should appear in constructor
        Assert.Contains("global::System.Collections.Generic.IEnumerable<global::TestApp.IHandler> handlers", generatedCode);
        // Collection properties should be assigned in constructor
        Assert.Contains("Handlers = handlers", generatedCode);
        // Collection properties should have correct type
        Assert.Contains("IEnumerable<global::TestApp.IHandler> Handlers { get; }", generatedCode);
    }

    [Fact]
    public void Generator_WithNestedProvider_GeneratesProviderProperty()
    {
        var source = """
            using NexusLabs.Needlr;
            using NexusLabs.Needlr.Generators;

            [assembly: GenerateTypeRegistry]

            namespace TestApp
            {
                public interface IRepository { }
                public class Repository : IRepository { }

                [Provider]
                public interface IDataProvider
                {
                    IRepository Repository { get; }
                }

                [Provider]
                public interface IAppProvider
                {
                    IDataProvider DataProvider { get; }
                }
            }
            """;

        var generatedCode = RunGenerator(source);

        // Both providers should be generated
        Assert.Contains("class DataProvider", generatedCode);
        Assert.Contains("class AppProvider", generatedCode);
        // Nested provider should be a constructor parameter
        Assert.Contains("IDataProvider dataProvider", generatedCode);
        // Nested provider property should be generated
        Assert.Contains("IDataProvider DataProvider { get; }", generatedCode);
    }

    [Fact]
    public void Generator_WithMixedPropertyKinds_GeneratesCorrectConstructor()
    {
        var source = """
            using NexusLabs.Needlr;
            using NexusLabs.Needlr.Generators;
            using System.Collections.Generic;

            [assembly: GenerateTypeRegistry]

            namespace TestApp
            {
                public interface IRequiredService { }
                public interface IOptionalService { }
                public interface IHandler { }

                [Provider]
                public interface IServicesProvider
                {
                    IRequiredService Required { get; }
                    IOptionalService? Optional { get; }
                    IEnumerable<IHandler> Handlers { get; }
                }
            }
            """;

        var generatedCode = RunGenerator(source);

        // All property kinds should be in constructor
        Assert.Contains("IRequiredService required", generatedCode);
        Assert.Contains("IOptionalService? optional = null", generatedCode);
        Assert.Contains("IEnumerable<global::TestApp.IHandler> handlers", generatedCode);
        // All should be assigned
        Assert.Contains("Required = required", generatedCode);
        Assert.Contains("Optional = optional", generatedCode);
        Assert.Contains("Handlers = handlers", generatedCode);
    }

    [Fact]
    public void Generator_WithShorthandCollections_GeneratesCollectionProperty()
    {
        var source = """
            using NexusLabs.Needlr;
            using NexusLabs.Needlr.Generators;
            using System.Collections.Generic;

            [assembly: GenerateTypeRegistry]

            namespace TestApp
            {
                public interface IHandler { }

                [Provider(Collections = new[] { typeof(IHandler) })]
                public partial class HandlersProvider { }
            }
            """;

        var generatedCode = RunGenerator(source);

        // Interface should be generated with collection property
        Assert.Contains("interface IHandlersProvider", generatedCode);
        Assert.Contains("IEnumerable<global::TestApp.IHandler> Handlers { get; }", generatedCode);
    }

    [Fact]
    public void Generator_WithShorthandOptional_GeneratesNullableProperty()
    {
        var source = """
            using NexusLabs.Needlr;
            using NexusLabs.Needlr.Generators;

            [assembly: GenerateTypeRegistry]

            namespace TestApp
            {
                public interface IOptionalService { }

                [Provider(Optional = new[] { typeof(IOptionalService) })]
                public partial class OptionalProvider { }
            }
            """;

        var generatedCode = RunGenerator(source);

        // Interface should be generated with nullable property
        Assert.Contains("interface IOptionalProvider", generatedCode);
        Assert.Contains("IOptionalService? OptionalService { get; }", generatedCode);
        // Constructor should have nullable parameter with default
        Assert.Contains("IOptionalService? optionalService = null", generatedCode);
    }

    [Fact]
    public void Generator_WithFactoriesParameter_GeneratesFactoryProperty()
    {
        var source = """
            using NexusLabs.Needlr;
            using NexusLabs.Needlr.Generators;

            [assembly: GenerateTypeRegistry]

            namespace TestApp
            {
                public interface IOrderService { }
                public class OrderService : IOrderService 
                {
                    public OrderService(string orderId) { }
                }

                [Provider(Factories = new[] { typeof(IOrderService) })]
                public partial class OrderProvider { }
            }
            """;

        var generatedCode = RunGenerator(source);

        // Interface should have factory property
        Assert.Contains("IOrderServiceFactory OrderServiceFactory { get; }", generatedCode);
    }

    [Fact]
    public void Generator_WithFactoriesOnInterface_GeneratesFactoryProperty()
    {
        var source = """
            using NexusLabs.Needlr;
            using NexusLabs.Needlr.Generators;

            [assembly: GenerateTypeRegistry]

            namespace TestApp
            {
                public interface IOrderService { }
                public class OrderService : IOrderService 
                {
                    public OrderService(string orderId) { }
                }

                [Provider]
                public interface IOrderProvider
                {
                    IOrderServiceFactory OrderServiceFactory { get; }
                }
            }
            """;

        var generatedCode = RunGenerator(source);

        // Implementation should inject the factory
        Assert.Contains("IOrderServiceFactory orderServiceFactory", generatedCode);
        Assert.Contains("OrderServiceFactory = orderServiceFactory", generatedCode);
    }
}
