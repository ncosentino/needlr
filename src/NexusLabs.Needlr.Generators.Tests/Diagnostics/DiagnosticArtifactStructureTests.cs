using System;
using System.Linq;

using NexusLabs.Needlr.Generators.Models;

using Xunit;

using static NexusLabs.Needlr.Generators.Tests.Diagnostics.DiagnosticModelFactory;

namespace NexusLabs.Needlr.Generators.Tests.Diagnostics;

public sealed class DiagnosticArtifactStructureTests
{
    [Fact]
    public void CombinedService_RendersEveryRelationshipWithValidMermaid()
    {
        var discovery = new DiagnosticDiscoveryBuilder()
            .WithTypes(
                Type(
                    "global::TestApp.OrderService",
                    GeneratorLifetime.Scoped,
                    new[] { "global::TestApp.IOrderService" },
                    new[] { "global::TestApp.ILogger" },
                    new[] { "primary" }),
                Type("global::TestApp.ConsoleLogger", new[] { "global::TestApp.ILogger" }))
            .WithDecorators(Decorator("global::TestApp.CachingOrderService", "global::TestApp.IOrderService", 1))
            .WithFactories(Factory("global::TestApp.OrderService"))
            .WithInterceptedServices(
                Intercepted("global::TestApp.OrderService", new[] { "global::TestApp.AuditInterceptor" }))
            .WithHostedServices(HostedService("global::TestApp.OrderService"))
            .WithPlugins(Plugin("global::TestApp.OrderPlugin"))
            .Build();

        var referenced = DiagnosticArtifactRenderer.ReferencedAssembly(
            "Plugin.Assembly",
            PluginType(
                "global::Plugin.PluginOrderService",
                "PluginOrderService",
                new[] { "global::Plugin.IOrderService" },
                isDecorator: true,
                hasFactory: true,
                hasInterceptorProxy: true));

        var document = MarkdownDocument.Parse(DiagnosticArtifactRenderer.DependencyGraph(
            discovery,
            DiagnosticArtifactRenderer.NoFilter(),
            referenced));

        Assert.Equal(
            new[]
            {
                "Needlr Dependency Graph",
                "Referenced Plugin Assemblies",
                "Plugin.Assembly",
                "Service Dependencies",
                "Decorator Chains",
                "Intercepted Services",
                "Keyed Services",
                "Plugin Assemblies",
                "Factory Services",
                "Interface Mapping",
                "Complexity Metrics",
                "Dependency Details",
            },
            document.SectionTitles());

        foreach (var block in document.AllMermaidBlocks())
        {
            Assert.Empty(block.UnrecognizedLines);
            Assert.StartsWith("graph ", block.Direction);
            foreach (var edge in block.Edges)
            {
                Assert.Contains(edge.From, block.NodeIds());
                Assert.Contains(edge.To, block.NodeIds());
            }
        }

        var dependencies = document.Section("Service Dependencies").MermaidBlocks.Single();
        Assert.True(
            dependencies.HasEdge("OrderService", "ConsoleLogger"),
            "Expected an edge from the consuming service to the type providing its dependency");
        Assert.Equal(new[] { "Singleton", "Scoped" }, dependencies.Subgraphs.Select(sg => sg.Id).ToArray());

        var decoratorChain = document.Section("Decorator Chains").MermaidBlocks.Single();
        Assert.True(
            decoratorChain.HasEdge("CachingOrderService", "OrderService"),
            "Expected the decorator to point at the decorated implementation");

        var factories = document.Section("Factory Services").MermaidBlocks.Single();
        Assert.Equal("produces", factories.Edges.Single(e => e.From == "OrderServiceFactory").Label);

        var intercepted = document.Section("Intercepted Services").MermaidBlocks.Single();
        Assert.Equal(
            new[] { "wraps", null },
            intercepted.Edges.Where(e => e.From == "OrderService_Proxy").Select(e => e.Label).ToArray());
        Assert.True(
            intercepted.HasEdge("OrderService_Proxy", "AuditInterceptor"),
            "Expected the proxy to point at each applied interceptor");

        var keyed = document.Section("Keyed Services").MermaidBlocks.Single();
        Assert.Equal("primary", keyed.Subgraphs.Single().Label);
    }

    [Fact]
    public void CombinedService_RegistrationIndexCountsEveryArtifact()
    {
        var discovery = new DiagnosticDiscoveryBuilder()
            .WithTypes(
                Type(
                    "global::TestApp.OrderService",
                    GeneratorLifetime.Scoped,
                    new[] { "global::TestApp.IOrderService" },
                    Array.Empty<string>(),
                    new[] { "primary", "secondary" }))
            .WithDecorators(
                Decorator("global::TestApp.CachingOrderService", "global::TestApp.IOrderService", 1),
                Decorator("global::TestApp.LoggingOrderService", "global::TestApp.IOrderService", 2))
            .WithFactories(Factory("global::TestApp.OrderService"))
            .WithInterceptedServices(
                Intercepted("global::TestApp.OrderService", new[] { "global::TestApp.AuditInterceptor" }))
            .WithPlugins(Plugin("global::TestApp.OrderPlugin"))
            .Build();

        var document = MarkdownDocument.Parse(
            DiagnosticArtifactRenderer.RegistrationIndex(discovery, DiagnosticArtifactRenderer.NoFilter()));

        Assert.Equal(
            new[]
            {
                "Needlr Registration Index",
                "Services (1)",
                "Decorators (2)",
                "Intercepted Services (1)",
                "Factories (1)",
                "Plugins (1)",
                "Keyed Services (2)",
            },
            document.SectionTitles());

        Assert.Equal(
            "CachingOrderService → LoggingOrderService",
            document.Section("Decorators (2)").Tables.Single().Cell(0, "Decorator Chain"));
        Assert.Equal(
            new[] { "`\"primary\"`", "`\"secondary\"`" },
            document.Section("Keyed Services (2)").Tables.Single().Column("Key"));
    }

    [Fact]
    public void EmptyDiscovery_ProducesValidArtifactsWithoutOptionalSections()
    {
        var discovery = new DiagnosticDiscoveryBuilder().Build();
        var filter = DiagnosticArtifactRenderer.NoFilter();

        var dependencyGraph = MarkdownDocument.Parse(DiagnosticArtifactRenderer.DependencyGraph(discovery, filter));
        var registrationIndex = MarkdownDocument.Parse(DiagnosticArtifactRenderer.RegistrationIndex(discovery, filter));
        var lifetimeSummary = MarkdownDocument.Parse(DiagnosticArtifactRenderer.LifetimeSummary(discovery, filter));
        var optionsSummary = MarkdownDocument.Parse(DiagnosticArtifactRenderer.OptionsSummary(discovery, filter));

        Assert.Equal(
            new[] { "Needlr Dependency Graph", "Service Dependencies", "Complexity Metrics", "Dependency Details" },
            dependencyGraph.SectionTitles());

        var graph = dependencyGraph.Section("Service Dependencies").MermaidBlocks.Single();
        Assert.Empty(graph.Nodes);
        Assert.Empty(graph.Edges);
        Assert.Empty(graph.Subgraphs);
        Assert.Empty(graph.UnrecognizedLines);

        var metrics = dependencyGraph.Section("Complexity Metrics").Tables.Single();
        Assert.Equal(new[] { "0", "0", "0" }, metrics.Column("Value"));
        Assert.Empty(dependencyGraph.Section("Dependency Details").Tables.Single().Rows);

        Assert.Equal(new[] { "Needlr Registration Index", "Services (0)" }, registrationIndex.SectionTitles());
        Assert.Empty(registrationIndex.Section("Services (0)").Tables);

        Assert.Equal(new[] { "Needlr Lifetime Summary", "Registration Counts" }, lifetimeSummary.SectionTitles());
        var counts = lifetimeSummary.Section("Registration Counts").Tables.Single();
        Assert.Single(counts.Rows);
        Assert.Equal("(none)", counts.Cell(0, "Lifetime"));
        Assert.Equal("0", counts.Cell(0, "Count"));

        Assert.Equal(new[] { "Needlr Options Summary" }, optionsSummary.SectionTitles());
        Assert.Contains(
            "*No options classes discovered. Add `[Options]` attribute to configuration classes.*",
            optionsSummary.Section("Needlr Options Summary").Lines);
    }

    [Fact]
    public void KeyedService_WithMarkdownAndMermaidSensitiveCharacters_PreservesStructure()
    {
        var discovery = new DiagnosticDiscoveryBuilder()
            .WithTypes(
                Type(
                    "global::TestApp.OrderService",
                    GeneratorLifetime.Singleton,
                    Array.Empty<string>(),
                    Array.Empty<string>(),
                    new[] { "pipe|and\"quote" }))
            .Build();

        var filter = DiagnosticArtifactRenderer.NoFilter();
        var registrationIndex = MarkdownDocument.Parse(DiagnosticArtifactRenderer.RegistrationIndex(discovery, filter));
        var dependencyGraph = MarkdownDocument.Parse(DiagnosticArtifactRenderer.DependencyGraph(discovery, filter));

        var keyedTable = registrationIndex.Section("Keyed Services (1)").Tables.Single();
        Assert.Equal(4, keyedTable.Columns.Count);
        Assert.Equal(4, keyedTable.Rows.Single().Count);
        Assert.Equal("`\"pipe\\|and\"quote\"`", keyedTable.Cell(0, "Key"));

        var keyedGraph = dependencyGraph.Section("Keyed Services").MermaidBlocks.Single();
        var subgraph = keyedGraph.Subgraphs.Single();
        Assert.Empty(keyedGraph.UnrecognizedLines);
        Assert.Equal("key_pipe_and_quote", subgraph.Id);
        Assert.Equal("pipe|and#quot;quote", subgraph.Label);
    }

    [Fact]
    public void GenericAndNullableTypeNames_ProduceValidMermaidIdentifiers()
    {
        var discovery = new DiagnosticDiscoveryBuilder()
            .WithTypes(
                Type(
                    "global::TestApp.Repository<global::TestApp.Order?>",
                    new[] { "global::TestApp.IRepository<global::TestApp.Order?>" }),
                Type(
                    "global::TestApp.Cache<global::System.String, global::System.Int32>",
                    Array.Empty<string>()))
            .Build();

        var document = MarkdownDocument.Parse(
            DiagnosticArtifactRenderer.DependencyGraph(discovery, DiagnosticArtifactRenderer.NoFilter()));

        var graph = document.Section("Service Dependencies").MermaidBlocks.Single();

        Assert.Empty(graph.UnrecognizedLines);
        Assert.Equal(new[] { "Repository_Order__", "Cache_String__Int32_" }, graph.Nodes.Select(n => n.Id).ToArray());
        Assert.Equal(
            new[] { "Repository<Order?>", "Cache<String, Int32>" },
            graph.Nodes.Select(n => n.Label).ToArray());

        var mapping = document.Section("Interface Mapping").MermaidBlocks.Single();
        Assert.Empty(mapping.UnrecognizedLines);
        Assert.True(
            mapping.HasEdge("IRepository_Order__", "Repository_Order__"),
            "Expected the interface mapping edge to point from the interface to the implementation");
    }

    [Fact]
    public void RepeatedRendering_ProducesIdenticalOutput()
    {
        var discovery = new DiagnosticDiscoveryBuilder()
            .WithTypes(
                Type("global::TestApp.OrderService", new[] { "global::TestApp.IOrderService" }),
                Type("global::TestApp.PaymentService", new[] { "global::TestApp.IPaymentService" }))
            .WithDecorators(Decorator("global::TestApp.CachingOrderService", "global::TestApp.IOrderService"))
            .WithFactories(Factory("global::TestApp.PaymentService"))
            .WithOptions(NamedOptions("global::TestApp.DatabaseOptions", "Database", "Primary"))
            .Build();

        var filter = DiagnosticArtifactRenderer.NoFilter();

        Assert.Equal(
            DiagnosticArtifactRenderer.DependencyGraph(discovery, filter),
            DiagnosticArtifactRenderer.DependencyGraph(discovery, filter));
        Assert.Equal(
            DiagnosticArtifactRenderer.RegistrationIndex(discovery, filter),
            DiagnosticArtifactRenderer.RegistrationIndex(discovery, filter));
        Assert.Equal(
            DiagnosticArtifactRenderer.LifetimeSummary(discovery, filter),
            DiagnosticArtifactRenderer.LifetimeSummary(discovery, filter));
        Assert.Equal(
            DiagnosticArtifactRenderer.OptionsSummary(discovery, filter),
            DiagnosticArtifactRenderer.OptionsSummary(discovery, filter));
    }

    [Fact]
    public void ReferencedAssemblyWithoutTypes_OmitsSection()
    {
        var referenced = DiagnosticArtifactRenderer.ReferencedAssembly("Plugin.Assembly");
        var discovery = new DiagnosticDiscoveryBuilder().Build();
        var filter = DiagnosticArtifactRenderer.NoFilter();

        var dependencyGraph = MarkdownDocument.Parse(
            DiagnosticArtifactRenderer.DependencyGraph(discovery, filter, referenced));
        var lifetimeSummary = MarkdownDocument.Parse(
            DiagnosticArtifactRenderer.LifetimeSummary(discovery, filter, referenced));
        var registrationIndex = MarkdownDocument.Parse(
            DiagnosticArtifactRenderer.RegistrationIndex(discovery, filter, referenced));

        Assert.False(
            dependencyGraph.HasSection("Referenced Plugin Assemblies"),
            "Expected the dependency graph to omit an assembly that contributes no types");
        Assert.False(
            lifetimeSummary.HasSection("Referenced Plugin Assemblies"),
            "Expected the lifetime summary to omit an assembly that contributes no types");
        Assert.False(
            registrationIndex.HasSection("Referenced Plugin Assemblies"),
            "Expected the registration index to omit an assembly that contributes no types");
    }

    [Fact]
    public void ReferencedAssemblyTypes_RenderLifetimeBreakdownAndDependencyEdges()
    {
        var referenced = DiagnosticArtifactRenderer.ReferencedAssembly(
            "Plugin.Assembly",
            new DiagnosticTypeInfo(
                "global::Plugin.PluginOrderService",
                "PluginOrderService",
                GeneratorLifetime.Singleton,
                new[] { "global::Plugin.IOrderService" },
                new[] { "global::Plugin.IPluginLogger" },
                false,
                false,
                false,
                null),
            new DiagnosticTypeInfo(
                "global::Plugin.PluginLogger",
                "PluginLogger",
                GeneratorLifetime.Transient,
                new[] { "global::Plugin.IPluginLogger" },
                Array.Empty<string>(),
                false,
                false,
                false,
                null));

        var discovery = new DiagnosticDiscoveryBuilder().Build();
        var filter = DiagnosticArtifactRenderer.NoFilter();

        var graph = MarkdownDocument.Parse(DiagnosticArtifactRenderer.DependencyGraph(discovery, filter, referenced))
            .Section("Plugin.Assembly")
            .MermaidBlocks
            .Single();

        Assert.Empty(graph.UnrecognizedLines);
        Assert.Equal(new[] { "Singleton", "Transient" }, graph.Subgraphs.Select(sg => sg.Id).ToArray());
        Assert.True(
            graph.HasEdge("PluginOrderService", "PluginLogger"),
            "Expected an intra-assembly dependency edge resolved through the dependency interface");

        var lifetimeTable = MarkdownDocument
            .Parse(DiagnosticArtifactRenderer.LifetimeSummary(discovery, filter, referenced))
            .Section("Plugin.Assembly")
            .Tables
            .Single();

        Assert.Equal(new[] { "1", "0", "1", "**2**" }, lifetimeTable.Column("Count"));
        Assert.Equal(new[] { "50%", "0%", "50%", "100%" }, lifetimeTable.Column("%"));
    }
}
