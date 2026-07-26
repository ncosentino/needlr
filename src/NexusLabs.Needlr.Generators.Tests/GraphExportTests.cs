using System.Globalization;
using System.Text.Json;

using NexusLabs.Needlr.Generators.Export;
using NexusLabs.Needlr.Generators.Models;

using Xunit;

namespace NexusLabs.Needlr.Generators.Tests;

public sealed class GraphExportTests
{
    [Fact]
    public void WhenGraphExportEnabled_GeneratesNeedlrGraphSource()
    {
        var source = @"
using NexusLabs.Needlr;
using NexusLabs.Needlr.Generators;

[assembly: GenerateTypeRegistry]

namespace TestApp
{
    public interface IUserService { }

    [Singleton]
    public class UserService : IUserService
    {
        public UserService(ILogger logger) { }
    }

    public interface ILogger { }

    [Singleton]
    public class ConsoleLogger : ILogger { }
}
";

        var files = GeneratorTestRunner.ForTypeRegistry()
            .WithSource(source)
            .WithGraphExportEnabled()
            .RunTypeRegistryGeneratorFiles();

        var graphFile = files.FirstOrDefault(f => f.FilePath.EndsWith("NeedlrGraph.g.cs"));
        Assert.NotNull(graphFile);

        var graphSource = graphFile.Content;
        Assert.Contains("NeedlrGraphExport", graphSource);
        Assert.Contains("GraphJson", graphSource);
        Assert.Contains("schemaVersion", graphSource);
        Assert.Contains("UserService", graphSource);
        Assert.Contains("ConsoleLogger", graphSource);
    }

    [Fact]
    public void WhenGraphExportDisabled_DoesNotGenerateNeedlrGraphSource()
    {
        var source = @"
using NexusLabs.Needlr;
using NexusLabs.Needlr.Generators;

[assembly: GenerateTypeRegistry]

namespace TestApp
{
    public interface IUserService { }

    [Singleton]
    public class UserService : IUserService { }
}
";

        var files = GeneratorTestRunner.ForTypeRegistry()
            .WithSource(source)
            .RunTypeRegistryGeneratorFiles();

        var graphFile = files.FirstOrDefault(f => f.FilePath.EndsWith("NeedlrGraph.g.cs"));
        Assert.Null(graphFile);
    }

    [Fact]
    public void GraphExport_IncludesDependencyInformation()
    {
        var source = @"
using NexusLabs.Needlr;
using NexusLabs.Needlr.Generators;

[assembly: GenerateTypeRegistry]

namespace TestApp
{
    public interface IOrderService { }
    public interface IPaymentService { }
    public interface IInventoryService { }

    [Scoped]
    public class OrderService : IOrderService
    {
        public OrderService(IPaymentService payment, IInventoryService inventory) { }
    }

    [Singleton]
    public class PaymentService : IPaymentService { }

    [Transient]
    public class InventoryService : IInventoryService { }
}
";

        var files = GeneratorTestRunner.ForTypeRegistry()
            .WithSource(source)
            .WithGraphExportEnabled()
            .RunTypeRegistryGeneratorFiles();

        var graphFile = files.FirstOrDefault(f => f.FilePath.EndsWith("NeedlrGraph.g.cs"));
        Assert.NotNull(graphFile);
        
        var graphSource = graphFile.Content;
        
        // Verify services are included
        Assert.Contains("OrderService", graphSource);
        Assert.Contains("PaymentService", graphSource);
        Assert.Contains("InventoryService", graphSource);
        
        // Verify lifetimes are included
        Assert.Contains("Scoped", graphSource);
        Assert.Contains("Singleton", graphSource);
        Assert.Contains("Transient", graphSource);
        
        // Verify dependencies are included (by type name)
        Assert.Contains("IPaymentService", graphSource);
        Assert.Contains("IInventoryService", graphSource);
    }

    [Fact]
    public void GraphExport_IncludesStatistics()
    {
        var source = @"
using NexusLabs.Needlr;
using NexusLabs.Needlr.Generators;

[assembly: GenerateTypeRegistry]

namespace TestApp
{
    public interface IService1 { }
    public interface IService2 { }
    public interface IService3 { }

    [Singleton] public class Service1 : IService1 { }
    [Singleton] public class Service2 : IService2 { }
    [Scoped] public class Service3 : IService3 { }
}
";

        var files = GeneratorTestRunner.ForTypeRegistry()
            .WithSource(source)
            .WithGraphExportEnabled()
            .RunTypeRegistryGeneratorFiles();

        var graphFile = files.FirstOrDefault(f => f.FilePath.EndsWith("NeedlrGraph.g.cs"));
        Assert.NotNull(graphFile);
        
        var graphSource = graphFile.Content;
        
        // Verify statistics section exists
        Assert.Contains("statistics", graphSource);
        Assert.Contains("totalServices", graphSource);
        Assert.Contains("singletons", graphSource);
        Assert.Contains("scoped", graphSource);
    }

    [Fact]
    public void GraphExport_IncludesDecoratorInformation()
    {
        var source = @"
using NexusLabs.Needlr;
using NexusLabs.Needlr.Generators;

[assembly: GenerateTypeRegistry]

namespace TestApp
{
    public interface IUserService { void DoWork(); }

    [Singleton]
    public class UserService : IUserService
    {
        public void DoWork() { }
    }

    [DecoratorFor<IUserService>(Order = 1)]
    public class LoggingDecorator : IUserService
    {
        private readonly IUserService _inner;
        public LoggingDecorator(IUserService inner) => _inner = inner;
        public void DoWork() => _inner.DoWork();
    }
}
";

        var files = GeneratorTestRunner.ForTypeRegistry()
            .WithSource(source)
            .WithGraphExportEnabled()
            .RunTypeRegistryGeneratorFiles();

        var graphFile = files.FirstOrDefault(f => f.FilePath.EndsWith("NeedlrGraph.g.cs"));
        Assert.NotNull(graphFile);
        
        var graphSource = graphFile.Content;
        
        // Verify decorator is mentioned
        Assert.Contains("LoggingDecorator", graphSource);
        Assert.Contains("decorators", graphSource);
    }

    [Fact]
    public void ProducerGraph_IncludesItsOwnServiceAndInterfaceLocations()
    {
        var files = GeneratorTestRunner.ForTypeRegistry()
            .WithSourceFile(
                "Feature/AssemblyInfo.cs",
                """
                [assembly: NexusLabs.Needlr.Generators.GenerateTypeRegistry]
                """)
            .WithSourceFile(
                "Contracts/IFeatureService.cs",
                """
                namespace Feature;

                public interface IFeatureService
                {
                }
                """)
            .WithSourceFile(
                "Services/FeatureService.cs",
                """
                namespace Feature;

                public sealed class FeatureService : IFeatureService
                {
                }
                """)
            .WithGraphExportEnabled()
            .RunTypeRegistryGeneratorFiles();

        var graphFile = Assert.Single(
            files,
            file => file.FilePath.EndsWith("NeedlrGraph.g.cs"));
        Assert.Contains(
            "Services/FeatureService.cs",
            graphFile.Content);
        Assert.Contains(
            "Contracts/IFeatureService.cs",
            graphFile.Content);
    }

    [Fact]
    public void EmptyDiscovery_ProducesParsableGraphWithEmptyCollections()
    {
        var json = GraphExporter.GenerateGraphJson(
            new GraphDiscoveryResultBuilder().Build(),
            "TestAssembly",
            projectPath: null,
            diagnostics: null,
            referencedAssemblyTypes: null);

        var root = ParseGraph(json);

        Assert.Equal("1.0", root.GetProperty("schemaVersion").GetString());
        Assert.True(
            DateTimeOffset.TryParse(
                root.GetProperty("generatedAt").GetString(),
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out _),
            "Expected generatedAt to be a round-trippable ISO 8601 timestamp");
        Assert.Equal(JsonValueKind.Null, root.GetProperty("projectPath").ValueKind);
        Assert.Equal("TestAssembly", root.GetProperty("assemblyName").GetString());
        Assert.Empty(root.GetProperty("services").EnumerateArray());
        Assert.Empty(root.GetProperty("diagnostics").EnumerateArray());

        var statistics = root.GetProperty("statistics");
        Assert.Equal(0, statistics.GetProperty("totalServices").GetInt32());
        Assert.Equal(0, statistics.GetProperty("singletons").GetInt32());
        Assert.Equal(0, statistics.GetProperty("scoped").GetInt32());
        Assert.Equal(0, statistics.GetProperty("transient").GetInt32());
        Assert.Equal(0, statistics.GetProperty("decorators").GetInt32());
        Assert.Equal(0, statistics.GetProperty("interceptors").GetInt32());
        Assert.Equal(0, statistics.GetProperty("factories").GetInt32());
        Assert.Equal(0, statistics.GetProperty("options").GetInt32());
        Assert.Equal(0, statistics.GetProperty("hostedServices").GetInt32());
        Assert.Equal(0, statistics.GetProperty("plugins").GetInt32());
    }

    [Fact]
    public void DiagnosticWithSourceLocation_EmitsLocationAndRelatedServices()
    {
        var json = GraphExporter.GenerateGraphJson(
            new GraphDiscoveryResultBuilder().Build(),
            "TestAssembly",
            "/src/TestAssembly",
            diagnostics:
            [
                new CollectedDiagnostic
                {
                    Id = "NDLRGEN022",
                    Severity = "Warning",
                    Message = "Captive dependency detected",
                    FilePath = "Services/OrderService.cs",
                    Line = 42,
                    RelatedServices =
                    [
                        "global::TestApp.OrderService",
                        "global::TestApp.PaymentService",
                    ],
                },
            ],
            referencedAssemblyTypes: null);

        var root = ParseGraph(json);
        Assert.Equal("/src/TestAssembly", root.GetProperty("projectPath").GetString());

        var diagnostic = Assert.Single(
            root.GetProperty("diagnostics").EnumerateArray());
        Assert.Equal("NDLRGEN022", diagnostic.GetProperty("id").GetString());
        Assert.Equal("Warning", diagnostic.GetProperty("severity").GetString());
        Assert.Equal(
            "Captive dependency detected",
            diagnostic.GetProperty("message").GetString());

        var location = diagnostic.GetProperty("location");
        Assert.Equal(JsonValueKind.Object, location.ValueKind);
        Assert.Equal(
            "Services/OrderService.cs",
            location.GetProperty("filePath").GetString());
        Assert.Equal(42, location.GetProperty("line").GetInt32());
        Assert.Equal(0, location.GetProperty("column").GetInt32());

        var relatedServices = diagnostic
            .GetProperty("relatedServices")
            .EnumerateArray()
            .Select(related => related.GetString())
            .ToList();
        Assert.Equal(
            new[]
            {
                "global::TestApp.OrderService",
                "global::TestApp.PaymentService",
            },
            relatedServices);
    }

    [Fact]
    public void DiagnosticWithoutLocationOrRelatedServices_EmitsNullLocationAndEmptyArray()
    {
        var json = GraphExporter.GenerateGraphJson(
            new GraphDiscoveryResultBuilder().Build(),
            "TestAssembly",
            projectPath: null,
            diagnostics:
            [
                new CollectedDiagnostic
                {
                    Id = "NDLRGEN001",
                    Severity = "Info",
                    Message = "No source location available",
                },
            ],
            referencedAssemblyTypes: null);

        var diagnostic = Assert.Single(
            ParseGraph(json).GetProperty("diagnostics").EnumerateArray());
        Assert.Equal(
            JsonValueKind.Null,
            diagnostic.GetProperty("location").ValueKind);
        Assert.Empty(diagnostic.GetProperty("relatedServices").EnumerateArray());
    }

    [Fact]
    public void MultipleServicesAndDiagnostics_SerializeWithValidCommaPlacement()
    {
        var discoveryResult = new GraphDiscoveryResultBuilder()
            .WithInjectableType(CreateType(
                "global::TestApp.First",
                GeneratorLifetime.Singleton))
            .WithInjectableType(CreateType(
                "global::TestApp.Second",
                GeneratorLifetime.Scoped))
            .WithInjectableType(CreateType(
                "global::TestApp.Third",
                GeneratorLifetime.Transient))
            .Build();

        var json = GraphExporter.GenerateGraphJson(
            discoveryResult,
            "TestAssembly",
            projectPath: null,
            diagnostics:
            [
                CreateDiagnostic("NDLRGEN001"),
                CreateDiagnostic("NDLRGEN002"),
                CreateDiagnostic("NDLRGEN003"),
            ],
            referencedAssemblyTypes: null);

        var root = ParseGraph(json);

        var serviceIds = root
            .GetProperty("services")
            .EnumerateArray()
            .Select(service => service.GetProperty("id").GetString())
            .ToList();
        Assert.Equal(
            new[]
            {
                "global::TestApp.First",
                "global::TestApp.Second",
                "global::TestApp.Third",
            },
            serviceIds);

        var diagnosticIds = root
            .GetProperty("diagnostics")
            .EnumerateArray()
            .Select(diagnostic => diagnostic.GetProperty("id").GetString())
            .ToList();
        Assert.Equal(
            new[] { "NDLRGEN001", "NDLRGEN002", "NDLRGEN003" },
            diagnosticIds);

        var statistics = root.GetProperty("statistics");
        Assert.Equal(3, statistics.GetProperty("totalServices").GetInt32());
        Assert.Equal(1, statistics.GetProperty("singletons").GetInt32());
        Assert.Equal(1, statistics.GetProperty("scoped").GetInt32());
        Assert.Equal(1, statistics.GetProperty("transient").GetInt32());
    }

    [Fact]
    public void ContentRequiringEscaping_RoundTripsThroughJson()
    {
        var messageNeedingEscapes =
            "Quote \" backslash \\ newline \n carriage \r tab \t done";
        var pathNeedingEscapes = "C:\\src\\\"Odd Project\"\\Service.cs";
        var keyNeedingEscapes = "key\"with\\escapes";

        var discoveryResult = new GraphDiscoveryResultBuilder()
            .WithInjectableType(new DiscoveredType(
                "global::TestApp.Escaped",
                ["global::TestApp.IEscaped"],
                "TestAssembly",
                GeneratorLifetime.Singleton,
                [
                    new TypeDiscoveryHelper.ConstructorParameterInfo(
                        "global::TestApp.IEscaped",
                        keyNeedingEscapes,
                        "escaped"),
                ],
                [keyNeedingEscapes],
                pathNeedingEscapes,
                7,
                false,
                [new InterfaceInfo("global::TestApp.IEscaped", pathNeedingEscapes, 3)]))
            .Build();

        var json = GraphExporter.GenerateGraphJson(
            discoveryResult,
            "TestAssembly",
            pathNeedingEscapes,
            diagnostics:
            [
                new CollectedDiagnostic
                {
                    Id = "NDLRGEN022",
                    Severity = "Error",
                    Message = messageNeedingEscapes,
                    FilePath = pathNeedingEscapes,
                    Line = 11,
                },
            ],
            referencedAssemblyTypes: null);

        var root = ParseGraph(json);
        Assert.Equal(pathNeedingEscapes, root.GetProperty("projectPath").GetString());

        var service = Assert.Single(root.GetProperty("services").EnumerateArray());
        Assert.Equal(
            pathNeedingEscapes,
            service.GetProperty("location").GetProperty("filePath").GetString());
        Assert.Equal(
            keyNeedingEscapes,
            Assert.Single(service.GetProperty("serviceKeys").EnumerateArray())
                .GetString());
        Assert.Equal(
            keyNeedingEscapes,
            Assert.Single(service.GetProperty("dependencies").EnumerateArray())
                .GetProperty("serviceKey")
                .GetString());
        Assert.Equal(
            pathNeedingEscapes,
            Assert.Single(service.GetProperty("interfaces").EnumerateArray())
                .GetProperty("location")
                .GetProperty("filePath")
                .GetString());

        var diagnostic = Assert.Single(
            root.GetProperty("diagnostics").EnumerateArray());
        Assert.Equal(
            messageNeedingEscapes,
            diagnostic.GetProperty("message").GetString());
        Assert.Equal(
            pathNeedingEscapes,
            diagnostic.GetProperty("location").GetProperty("filePath").GetString());
    }

    [Fact]
    public void ReferencedAssemblyTypes_AppearInServicesAndStatistics()
    {
        var discoveryResult = new GraphDiscoveryResultBuilder()
            .WithInjectableType(CreateType(
                "global::TestApp.HostService",
                GeneratorLifetime.Singleton))
            .Build();

        var json = GraphExporter.GenerateGraphJson(
            discoveryResult,
            "TestAssembly",
            projectPath: null,
            diagnostics: null,
            referencedAssemblyTypes: new Dictionary<string, IReadOnlyList<DiscoveredType>>
            {
                ["Feature"] =
                [
                    CreateType("global::Feature.FeatureService", GeneratorLifetime.Scoped),
                    CreateType("global::Feature.OtherService", GeneratorLifetime.Transient),
                ],
            });

        var root = ParseGraph(json);
        var services = root.GetProperty("services").EnumerateArray().ToList();
        Assert.Equal(3, services.Count);
        Assert.Equal("TestAssembly", services[0].GetProperty("assemblyName").GetString());
        Assert.Equal("Feature", services[1].GetProperty("assemblyName").GetString());
        Assert.Equal("Feature", services[2].GetProperty("assemblyName").GetString());

        var statistics = root.GetProperty("statistics");
        Assert.Equal(3, statistics.GetProperty("totalServices").GetInt32());
        Assert.Equal(1, statistics.GetProperty("singletons").GetInt32());
        Assert.Equal(1, statistics.GetProperty("scoped").GetInt32());
        Assert.Equal(1, statistics.GetProperty("transient").GetInt32());
    }

    [Fact]
    public void DependenciesResolveAcrossCurrentAndReferencedAssemblies()
    {
        var discoveryResult = new GraphDiscoveryResultBuilder()
            .WithInjectableType(new DiscoveredType(
                "global::TestApp.HostService",
                ["global::TestApp.IHostService"],
                "TestAssembly",
                GeneratorLifetime.Singleton,
                [
                    new TypeDiscoveryHelper.ConstructorParameterInfo(
                        "global::Feature.IFeatureService",
                        parameterName: "feature"),
                    new TypeDiscoveryHelper.ConstructorParameterInfo(
                        "global::Missing.IUnknownService",
                        parameterName: "unknown"),
                ],
                []))
            .Build();

        var featureService = new DiscoveredType(
            "global::Feature.FeatureService",
            ["global::Feature.IFeatureService"],
            "Feature",
            GeneratorLifetime.Scoped,
            [
                new TypeDiscoveryHelper.ConstructorParameterInfo(
                    "global::TestApp.IHostService",
                    parameterName: "host"),
                new TypeDiscoveryHelper.ConstructorParameterInfo(
                    "global::Support.ISupportService",
                    parameterName: "support"),
            ],
            []);
        var supportService = new DiscoveredType(
            "global::Support.SupportService",
            ["global::Support.ISupportService"],
            "Support",
            GeneratorLifetime.Transient,
            [],
            []);

        var json = GraphExporter.GenerateGraphJson(
            discoveryResult,
            "TestAssembly",
            projectPath: null,
            diagnostics: null,
            referencedAssemblyTypes: new Dictionary<string, IReadOnlyList<DiscoveredType>>
            {
                ["Feature"] = [featureService],
                ["Support"] = [supportService],
            });

        var services = ParseGraph(json).GetProperty("services").EnumerateArray().ToList();
        Assert.Equal(3, services.Count);

        var hostDependencies = services[0]
            .GetProperty("dependencies")
            .EnumerateArray()
            .ToList();
        Assert.Equal(2, hostDependencies.Count);
        Assert.Equal(
            "global::Feature.FeatureService",
            hostDependencies[0].GetProperty("resolvedTo").GetString());
        Assert.Equal(
            "Scoped",
            hostDependencies[0].GetProperty("resolvedLifetime").GetString());
        Assert.Equal(
            JsonValueKind.Null,
            hostDependencies[1].GetProperty("resolvedTo").ValueKind);
        Assert.Equal(
            JsonValueKind.Null,
            hostDependencies[1].GetProperty("resolvedLifetime").ValueKind);
        Assert.False(
            hostDependencies[1].GetProperty("isKeyed").GetBoolean(),
            "Expected an unkeyed dependency to report isKeyed false");

        var featureDependencies = services[1]
            .GetProperty("dependencies")
            .EnumerateArray()
            .ToList();
        Assert.Equal(2, featureDependencies.Count);
        Assert.Equal(
            "global::TestApp.HostService",
            featureDependencies[0].GetProperty("resolvedTo").GetString());
        Assert.Equal(
            "Singleton",
            featureDependencies[0].GetProperty("resolvedLifetime").GetString());
        Assert.Equal(
            "global::Support.SupportService",
            featureDependencies[1].GetProperty("resolvedTo").GetString());
        Assert.Equal(
            "Transient",
            featureDependencies[1].GetProperty("resolvedLifetime").GetString());
    }

    [Fact]
    public void DuplicateTypeAndInterfaceNames_ResolveWithCurrentAssemblyPrecedence()
    {
        var discoveryResult = new GraphDiscoveryResultBuilder()
            .WithInjectableType(new DiscoveredType(
                "global::Shared.SharedService",
                ["global::Shared.ISharedService"],
                "TestAssembly",
                GeneratorLifetime.Singleton,
                [],
                []))
            .WithInjectableType(new DiscoveredType(
                "global::TestApp.SecondSharedImplementation",
                ["global::Shared.ISharedService"],
                "TestAssembly",
                GeneratorLifetime.Transient,
                [],
                []))
            .WithInjectableType(new DiscoveredType(
                "global::TestApp.Consumer",
                [],
                "TestAssembly",
                GeneratorLifetime.Scoped,
                [
                    new TypeDiscoveryHelper.ConstructorParameterInfo(
                        "global::Shared.ISharedService",
                        parameterName: "shared"),
                    new TypeDiscoveryHelper.ConstructorParameterInfo(
                        "global::Shared.SharedService",
                        parameterName: "concrete"),
                ],
                []))
            .Build();

        var json = GraphExporter.GenerateGraphJson(
            discoveryResult,
            "TestAssembly",
            projectPath: null,
            diagnostics: null,
            referencedAssemblyTypes: new Dictionary<string, IReadOnlyList<DiscoveredType>>
            {
                ["Shared"] =
                [
                    new DiscoveredType(
                        "global::Shared.SharedService",
                        ["global::Shared.ISharedService"],
                        "Shared",
                        GeneratorLifetime.Transient,
                        [],
                        []),
                ],
            });

        var services = ParseGraph(json).GetProperty("services").EnumerateArray().ToList();
        Assert.Equal(4, services.Count);

        var consumerDependencies = services[2]
            .GetProperty("dependencies")
            .EnumerateArray()
            .ToList();
        Assert.Equal(2, consumerDependencies.Count);
        Assert.Equal(
            "global::Shared.SharedService",
            consumerDependencies[0].GetProperty("resolvedTo").GetString());
        Assert.Equal(
            "Singleton",
            consumerDependencies[0].GetProperty("resolvedLifetime").GetString());
        Assert.Equal(
            "global::Shared.SharedService",
            consumerDependencies[1].GetProperty("resolvedTo").GetString());
        Assert.Equal(
            "Singleton",
            consumerDependencies[1].GetProperty("resolvedLifetime").GetString());
    }

    [Fact]
    public void ReferencedTypesWithoutSourceLocations_EmitNullLocations()
    {
        var json = GraphExporter.GenerateGraphJson(
            new GraphDiscoveryResultBuilder().Build(),
            "TestAssembly",
            projectPath: null,
            diagnostics: null,
            referencedAssemblyTypes: new Dictionary<string, IReadOnlyList<DiscoveredType>>
            {
                ["Feature"] =
                [
                    new DiscoveredType(
                        "global::Feature.FeatureService",
                        ["global::Feature.IFeatureService"],
                        "Feature",
                        GeneratorLifetime.Singleton,
                        [],
                        [],
                        null,
                        0,
                        false,
                        [new InterfaceInfo("global::Feature.IFeatureService")]),
                ],
            });

        var service = Assert.Single(
            ParseGraph(json).GetProperty("services").EnumerateArray());
        Assert.Equal(JsonValueKind.Null, service.GetProperty("location").ValueKind);

        var interfaceEntry = Assert.Single(
            service.GetProperty("interfaces").EnumerateArray());
        Assert.Equal(
            "global::Feature.IFeatureService",
            interfaceEntry.GetProperty("fullName").GetString());
        Assert.Equal(
            "IFeatureService",
            interfaceEntry.GetProperty("name").GetString());
        Assert.Equal(
            JsonValueKind.Null,
            interfaceEntry.GetProperty("location").ValueKind);
    }

    [Fact]
    public void ProducerOwnedLocations_EmitConcreteAndInterfaceLocations()
    {
        var discoveryResult = new GraphDiscoveryResultBuilder()
            .WithInjectableType(new DiscoveredType(
                "global::Feature.FeatureService",
                ["global::Feature.IFeatureService"],
                "TestAssembly",
                GeneratorLifetime.Singleton,
                [],
                [],
                "Services/FeatureService.cs",
                12,
                false,
                [
                    new InterfaceInfo(
                        "global::Feature.IFeatureService",
                        "Contracts/IFeatureService.cs",
                        3),
                ]))
            .Build();

        var json = GraphExporter.GenerateGraphJson(
            discoveryResult,
            "TestAssembly",
            projectPath: null,
            diagnostics: null,
            referencedAssemblyTypes: null);

        var service = Assert.Single(
            ParseGraph(json).GetProperty("services").EnumerateArray());
        var location = service.GetProperty("location");
        Assert.Equal(
            "Services/FeatureService.cs",
            location.GetProperty("filePath").GetString());
        Assert.Equal(12, location.GetProperty("line").GetInt32());
        Assert.Equal(0, location.GetProperty("column").GetInt32());

        var interfaceLocation = Assert.Single(
            service.GetProperty("interfaces").EnumerateArray())
            .GetProperty("location");
        Assert.Equal(
            "Contracts/IFeatureService.cs",
            interfaceLocation.GetProperty("filePath").GetString());
        Assert.Equal(3, interfaceLocation.GetProperty("line").GetInt32());
    }

    [Fact]
    public void TypesWithoutInterfaceInfos_FallBackToInterfaceNames()
    {
        var discoveryResult = new GraphDiscoveryResultBuilder()
            .WithInjectableType(new DiscoveredType(
                "global::TestApp.LegacyService",
                ["global::TestApp.ILegacyService", "global::TestApp.IAlsoLegacy"],
                "TestAssembly",
                GeneratorLifetime.Singleton,
                [],
                []))
            .Build();

        var json = GraphExporter.GenerateGraphJson(
            discoveryResult,
            "TestAssembly",
            projectPath: null,
            diagnostics: null,
            referencedAssemblyTypes: null);

        var interfaces = Assert.Single(
            ParseGraph(json).GetProperty("services").EnumerateArray())
            .GetProperty("interfaces")
            .EnumerateArray()
            .ToList();
        Assert.Equal(2, interfaces.Count);
        Assert.Equal("ILegacyService", interfaces[0].GetProperty("name").GetString());
        Assert.Equal(
            "global::TestApp.ILegacyService",
            interfaces[0].GetProperty("fullName").GetString());
        Assert.Equal(JsonValueKind.Null, interfaces[0].GetProperty("location").ValueKind);
        Assert.Equal("IAlsoLegacy", interfaces[1].GetProperty("name").GetString());
        Assert.Equal(JsonValueKind.Null, interfaces[1].GetProperty("location").ValueKind);
    }

    [Fact]
    public void ServiceWithFullMetadata_EmitsKeysDecoratorsInterceptorsAndFlags()
    {
        const string ServiceTypeName = "global::TestApp.OrderService";

        var discoveryResult = new GraphDiscoveryResultBuilder()
            .WithInjectableType(new DiscoveredType(
                ServiceTypeName,
                ["global::TestApp.IOrderService"],
                "TestAssembly",
                GeneratorLifetime.Scoped,
                [
                    new TypeDiscoveryHelper.ConstructorParameterInfo(
                        "global::TestApp.IPaymentService",
                        "primary",
                        "payment"),
                ],
                ["orders", "orders-secondary"],
                "Services/OrderService.cs",
                9,
                true))
            .WithFactory(new DiscoveredFactory(
                ServiceTypeName,
                [],
                "TestAssembly",
                1,
                []))
            .WithHostedService(new DiscoveredHostedService(
                ServiceTypeName,
                "TestAssembly",
                GeneratorLifetime.Singleton,
                []))
            .WithPlugin(new DiscoveredPlugin(
                ServiceTypeName,
                [],
                "TestAssembly",
                []))
            .WithOptions(new DiscoveredOptions(
                ServiceTypeName,
                "Orders",
                null,
                false,
                "TestAssembly"))
            .WithDecorator(new DiscoveredDecorator(
                "global::TestApp.CachingOrderDecorator",
                "global::TestApp.IOrderService",
                2,
                "TestAssembly"))
            .WithDecorator(new DiscoveredDecorator(
                "global::TestApp.LoggingOrderDecorator",
                "global::TestApp.IOrderService",
                1,
                "TestAssembly"))
            .WithDecorator(new DiscoveredDecorator(
                "global::TestApp.UnrelatedDecorator",
                "global::TestApp.IUnrelatedService",
                1,
                "TestAssembly"))
            .WithInterceptedService(new DiscoveredInterceptedService(
                ServiceTypeName,
                ["global::TestApp.IOrderService"],
                "TestAssembly",
                GeneratorLifetime.Scoped,
                [],
                ["global::TestApp.LoggingInterceptor", "global::TestApp.RetryInterceptor"]))
            .Build();

        var json = GraphExporter.GenerateGraphJson(
            discoveryResult,
            "TestAssembly",
            projectPath: null,
            diagnostics: null,
            referencedAssemblyTypes: null);

        var root = ParseGraph(json);
        var service = Assert.Single(root.GetProperty("services").EnumerateArray());

        Assert.Equal("Scoped", service.GetProperty("lifetime").GetString());
        Assert.Equal(
            new[] { "Scoped", "Keyed" },
            service.GetProperty("attributes")
                .EnumerateArray()
                .Select(attribute => attribute.GetString())
                .ToArray());
        Assert.Equal(
            new[] { "orders", "orders-secondary" },
            service.GetProperty("serviceKeys")
                .EnumerateArray()
                .Select(key => key.GetString())
                .ToArray());

        var dependency = Assert.Single(
            service.GetProperty("dependencies").EnumerateArray());
        Assert.Equal("payment", dependency.GetProperty("parameterName").GetString());
        Assert.Equal("IPaymentService", dependency.GetProperty("typeName").GetString());
        Assert.True(
            dependency.GetProperty("isKeyed").GetBoolean(),
            "Expected a [FromKeyedServices] parameter to report isKeyed true");
        Assert.Equal("primary", dependency.GetProperty("serviceKey").GetString());

        var decorators = service.GetProperty("decorators").EnumerateArray().ToList();
        Assert.Equal(2, decorators.Count);
        Assert.Equal(
            "global::TestApp.LoggingOrderDecorator",
            decorators[0].GetProperty("typeName").GetString());
        Assert.Equal(1, decorators[0].GetProperty("order").GetInt32());
        Assert.Equal(
            "global::TestApp.CachingOrderDecorator",
            decorators[1].GetProperty("typeName").GetString());
        Assert.Equal(2, decorators[1].GetProperty("order").GetInt32());

        Assert.Equal(
            new[]
            {
                "global::TestApp.LoggingInterceptor",
                "global::TestApp.RetryInterceptor",
            },
            service.GetProperty("interceptors")
                .EnumerateArray()
                .Select(interceptor => interceptor.GetString())
                .ToArray());

        var metadata = service.GetProperty("metadata");
        Assert.True(
            metadata.GetProperty("hasFactory").GetBoolean(),
            "Expected a service with a discovered factory to report hasFactory true");
        Assert.True(
            metadata.GetProperty("hasOptions").GetBoolean(),
            "Expected a service with a discovered options registration to report hasOptions true");
        Assert.True(
            metadata.GetProperty("isHostedService").GetBoolean(),
            "Expected a discovered hosted service to report isHostedService true");
        Assert.True(
            metadata.GetProperty("isDisposable").GetBoolean(),
            "Expected a disposable service to report isDisposable true");
        Assert.True(
            metadata.GetProperty("isPlugin").GetBoolean(),
            "Expected a discovered plugin to report isPlugin true");

        var statistics = root.GetProperty("statistics");
        Assert.Equal(3, statistics.GetProperty("decorators").GetInt32());
        Assert.Equal(1, statistics.GetProperty("interceptors").GetInt32());
        Assert.Equal(1, statistics.GetProperty("factories").GetInt32());
        Assert.Equal(1, statistics.GetProperty("options").GetInt32());
        Assert.Equal(1, statistics.GetProperty("hostedServices").GetInt32());
        Assert.Equal(1, statistics.GetProperty("plugins").GetInt32());
    }

    [Fact]
    public void ServiceWithoutMetadataMatches_EmitsFalseFlagsAndEmptyCollections()
    {
        var discoveryResult = new GraphDiscoveryResultBuilder()
            .WithInjectableType(CreateType(
                "global::TestApp.PlainService",
                GeneratorLifetime.Singleton))
            .WithFactory(new DiscoveredFactory(
                "global::TestApp.OtherService",
                [],
                "TestAssembly",
                1,
                []))
            .WithHostedService(new DiscoveredHostedService(
                "global::TestApp.OtherService",
                "TestAssembly",
                GeneratorLifetime.Singleton,
                []))
            .WithPlugin(new DiscoveredPlugin(
                "global::TestApp.OtherService",
                [],
                "TestAssembly",
                []))
            .WithOptions(new DiscoveredOptions(
                "global::TestApp.OtherOptions",
                "Other",
                null,
                false,
                "TestAssembly"))
            .Build();

        var json = GraphExporter.GenerateGraphJson(
            discoveryResult,
            "TestAssembly",
            projectPath: null,
            diagnostics: null,
            referencedAssemblyTypes: null);

        var service = Assert.Single(
            ParseGraph(json).GetProperty("services").EnumerateArray());
        Assert.Empty(service.GetProperty("interfaces").EnumerateArray());
        Assert.Empty(service.GetProperty("dependencies").EnumerateArray());
        Assert.Empty(service.GetProperty("decorators").EnumerateArray());
        Assert.Empty(service.GetProperty("interceptors").EnumerateArray());
        Assert.Empty(service.GetProperty("serviceKeys").EnumerateArray());
        Assert.Equal(
            new[] { "Singleton" },
            service.GetProperty("attributes")
                .EnumerateArray()
                .Select(attribute => attribute.GetString())
                .ToArray());

        var metadata = service.GetProperty("metadata");
        Assert.False(
            metadata.GetProperty("hasFactory").GetBoolean(),
            "Expected a service without a factory to report hasFactory false");
        Assert.False(
            metadata.GetProperty("hasOptions").GetBoolean(),
            "Expected a service without options to report hasOptions false");
        Assert.False(
            metadata.GetProperty("isHostedService").GetBoolean(),
            "Expected a service that is not hosted to report isHostedService false");
        Assert.False(
            metadata.GetProperty("isDisposable").GetBoolean(),
            "Expected a non-disposable service to report isDisposable false");
        Assert.False(
            metadata.GetProperty("isPlugin").GetBoolean(),
            "Expected a non-plugin service to report isPlugin false");
    }

    [Fact]
    public void ComplexTypeNames_AreSimplifiedForDisplayAndPreservedInFullNames()
    {
        var discoveryResult = new GraphDiscoveryResultBuilder()
            .WithInjectableType(new DiscoveredType(
                "global::GlobalNamespaceService",
                [],
                "TestAssembly",
                GeneratorLifetime.Singleton,
                [
                    new TypeDiscoveryHelper.ConstructorParameterInfo(
                        "global::System.Collections.Generic.IReadOnlyList<global::System.Collections.Generic.List<global::TestApp.Item>>",
                        parameterName: "nested"),
                    new TypeDiscoveryHelper.ConstructorParameterInfo(
                        "global::System.Collections.Generic.Dictionary<global::System.String, global::TestApp.Item>",
                        parameterName: "map"),
                    new TypeDiscoveryHelper.ConstructorParameterInfo(
                        "global::TestApp.Item[]",
                        parameterName: "items"),
                    new TypeDiscoveryHelper.ConstructorParameterInfo(
                        "global::TestApp.Item?",
                        parameterName: "maybeItem"),
                    new TypeDiscoveryHelper.ConstructorParameterInfo(
                        "global::RootLevelDependency",
                        parameterName: "rootLevel"),
                ],
                []))
            .Build();

        var json = GraphExporter.GenerateGraphJson(
            discoveryResult,
            "TestAssembly",
            projectPath: null,
            diagnostics: null,
            referencedAssemblyTypes: null);

        var service = Assert.Single(
            ParseGraph(json).GetProperty("services").EnumerateArray());
        Assert.Equal(
            "GlobalNamespaceService",
            service.GetProperty("typeName").GetString());
        Assert.Equal(
            "global::GlobalNamespaceService",
            service.GetProperty("fullTypeName").GetString());

        var dependencies = service.GetProperty("dependencies").EnumerateArray().ToList();
        Assert.Equal(5, dependencies.Count);
        Assert.Equal(
            "IReadOnlyList<List<Item>>",
            dependencies[0].GetProperty("typeName").GetString());
        Assert.Equal(
            "global::System.Collections.Generic.IReadOnlyList<global::System.Collections.Generic.List<global::TestApp.Item>>",
            dependencies[0].GetProperty("fullTypeName").GetString());
        Assert.Equal(
            "Dictionary<String, Item>",
            dependencies[1].GetProperty("typeName").GetString());
        Assert.Equal("Item[]", dependencies[2].GetProperty("typeName").GetString());
        Assert.Equal("Item?", dependencies[3].GetProperty("typeName").GetString());
        Assert.Equal(
            "RootLevelDependency",
            dependencies[4].GetProperty("typeName").GetString());
    }

    private static DiscoveredType CreateType(
        string typeName,
        GeneratorLifetime lifetime)
    {
        return new DiscoveredType(
            typeName,
            [],
            "TestAssembly",
            lifetime,
            [],
            []);
    }

    private static CollectedDiagnostic CreateDiagnostic(string id)
    {
        return new CollectedDiagnostic
        {
            Id = id,
            Severity = "Warning",
            Message = $"Diagnostic {id}",
            FilePath = $"Services/{id}.cs",
            Line = 1,
            RelatedServices = ["global::TestApp.First"],
        };
    }

    private static JsonElement ParseGraph(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }
}
