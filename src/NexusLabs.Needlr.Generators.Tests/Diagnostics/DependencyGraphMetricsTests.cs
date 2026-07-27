using System;
using System.Linq;

using Xunit;

using static NexusLabs.Needlr.Generators.Tests.Diagnostics.DiagnosticModelFactory;

namespace NexusLabs.Needlr.Generators.Tests.Diagnostics;

public sealed class DependencyGraphMetricsTests
{
    [Fact]
    public void LinearChain_ReportsChainLengthAsMaxDepth()
    {
        var discovery = new DiagnosticDiscoveryBuilder()
            .WithTypes(
                Service("Top", new[] { "global::TestApp.Middle" }),
                Service("Middle", new[] { "global::TestApp.Bottom" }),
                Service("Bottom", Array.Empty<string>()))
            .Build();

        Assert.Equal("3", MaxDependencyDepth(discovery));
    }

    [Fact]
    public void DisconnectedComponents_ReportDeepestComponentDepth()
    {
        var discovery = new DiagnosticDiscoveryBuilder()
            .WithTypes(
                Service("Top", new[] { "global::TestApp.Middle" }),
                Service("Middle", new[] { "global::TestApp.Bottom" }),
                Service("Bottom", Array.Empty<string>()),
                Service("Isolated", Array.Empty<string>()),
                Service("PairHead", new[] { "global::TestApp.PairTail" }),
                Service("PairTail", Array.Empty<string>()))
            .Build();

        var document = MarkdownDocument.Parse(
            DiagnosticArtifactRenderer.DependencyGraph(discovery, DiagnosticArtifactRenderer.NoFilter()));
        var graph = document.Section("Service Dependencies").MermaidBlocks.Single();

        Assert.Equal("3", document.Section("Complexity Metrics").Tables.Single().Cell(1, "Value"));
        Assert.Equal(6, graph.Nodes.Count);
        Assert.Equal(3, graph.Edges.Count);
        Assert.DoesNotContain(graph.Edges, e => e.From == "Isolated" || e.To == "Isolated");
    }

    [Fact]
    public void SharedDependency_IsRenderedOncePerDependentEdge()
    {
        var discovery = new DiagnosticDiscoveryBuilder()
            .WithTypes(
                Service("Left", new[] { "global::TestApp.Shared" }),
                Service("Right", new[] { "global::TestApp.Shared" }),
                Service("Shared", new[] { "global::TestApp.Leaf" }),
                Service("Leaf", Array.Empty<string>()))
            .Build();

        var document = MarkdownDocument.Parse(
            DiagnosticArtifactRenderer.DependencyGraph(discovery, DiagnosticArtifactRenderer.NoFilter()));
        var graph = document.Section("Service Dependencies").MermaidBlocks.Single();

        Assert.Equal("3", document.Section("Complexity Metrics").Tables.Single().Cell(1, "Value"));
        Assert.True(graph.HasEdge("Left", "Shared"), "Expected an edge from Left to the shared dependency");
        Assert.True(graph.HasEdge("Right", "Shared"), "Expected an edge from Right to the shared dependency");
        Assert.Equal(3, graph.Edges.Count);
    }

    [Fact]
    public void CyclicDependencies_TerminateAndRenderBothEdges()
    {
        var discovery = new DiagnosticDiscoveryBuilder()
            .WithTypes(
                Service("First", new[] { "global::TestApp.Second" }),
                Service("Second", new[] { "global::TestApp.First" }))
            .Build();

        var document = MarkdownDocument.Parse(
            DiagnosticArtifactRenderer.DependencyGraph(discovery, DiagnosticArtifactRenderer.NoFilter()));
        var graph = document.Section("Service Dependencies").MermaidBlocks.Single();

        Assert.Equal("2", document.Section("Complexity Metrics").Tables.Single().Cell(1, "Value"));
        Assert.True(graph.HasEdge("First", "Second"), "Expected the forward edge of the cycle");
        Assert.True(graph.HasEdge("Second", "First"), "Expected the reverse edge of the cycle");
    }

    [Fact]
    public void MissingDependencyTarget_IsListedButNotDrawnAsEdge()
    {
        var discovery = new DiagnosticDiscoveryBuilder()
            .WithTypes(Service("OrderService", new[] { "global::TestApp.IMissingService" }))
            .Build();

        var document = MarkdownDocument.Parse(
            DiagnosticArtifactRenderer.DependencyGraph(discovery, DiagnosticArtifactRenderer.NoFilter()));
        var graph = document.Section("Service Dependencies").MermaidBlocks.Single();

        Assert.Empty(graph.Edges);
        Assert.Equal("1", document.Section("Complexity Metrics").Tables.Single().Cell(1, "Value"));
        Assert.Equal(
            "IMissingService",
            document.Section("Dependency Details").Tables.Single().Cell(0, "Dependencies"));
    }

    [Fact]
    public void InterfaceProvidedDependency_ResolvesToImplementation()
    {
        var discovery = new DiagnosticDiscoveryBuilder()
            .WithTypes(
                Type(
                    "global::TestApp.OrderService",
                    GeneratorLifetime.Singleton,
                    Array.Empty<string>(),
                    new[] { "global::TestApp.ILogger" },
                    Array.Empty<string>()),
                Type("global::TestApp.ConsoleLogger", new[] { "global::TestApp.ILogger" }))
            .Build();

        var document = MarkdownDocument.Parse(
            DiagnosticArtifactRenderer.DependencyGraph(discovery, DiagnosticArtifactRenderer.NoFilter()));
        var graph = document.Section("Service Dependencies").MermaidBlocks.Single();

        Assert.True(
            graph.HasEdge("OrderService", "ConsoleLogger"),
            "Expected the dependency edge to resolve through the implemented interface");
        Assert.Equal("2", document.Section("Complexity Metrics").Tables.Single().Cell(1, "Value"));
    }

    [Fact]
    public void HubServices_AreListedWhenDependentThresholdIsReached()
    {
        var discovery = new DiagnosticDiscoveryBuilder()
            .WithTypes(
                Service("First", new[] { "global::TestApp.Shared" }),
                Service("Second", new[] { "global::TestApp.Shared" }),
                Service("Third", new[] { "global::TestApp.Shared" }),
                Service("Shared", Array.Empty<string>()))
            .Build();

        var document = MarkdownDocument.Parse(
            DiagnosticArtifactRenderer.DependencyGraph(discovery, DiagnosticArtifactRenderer.NoFilter()));
        var section = document.Section("Complexity Metrics");

        Assert.Equal("1", section.Tables.Single().Cell(2, "Value"));
        Assert.Contains("**Hub Services:** Shared (3)", section.Lines);
    }

    [Fact]
    public void HubServices_AreOmittedBelowDependentThreshold()
    {
        var discovery = new DiagnosticDiscoveryBuilder()
            .WithTypes(
                Service("First", new[] { "global::TestApp.Shared" }),
                Service("Second", new[] { "global::TestApp.Shared" }),
                Service("Shared", Array.Empty<string>()))
            .Build();

        var document = MarkdownDocument.Parse(
            DiagnosticArtifactRenderer.DependencyGraph(discovery, DiagnosticArtifactRenderer.NoFilter()));
        var section = document.Section("Complexity Metrics");

        Assert.Equal("0", section.Tables.Single().Cell(2, "Value"));
        Assert.DoesNotContain(section.Lines, line => line.StartsWith("**Hub Services:**", StringComparison.Ordinal));
    }

    private static Models.DiscoveredType Service(string shortName, string[] dependencies)
    {
        return Type(
            $"global::TestApp.{shortName}",
            GeneratorLifetime.Singleton,
            Array.Empty<string>(),
            dependencies,
            Array.Empty<string>());
    }

    private static string MaxDependencyDepth(Models.DiscoveryResult discovery)
    {
        return MarkdownDocument
            .Parse(DiagnosticArtifactRenderer.DependencyGraph(discovery, DiagnosticArtifactRenderer.NoFilter()))
            .Section("Complexity Metrics")
            .Tables
            .Single()
            .Cell(1, "Value");
    }
}
