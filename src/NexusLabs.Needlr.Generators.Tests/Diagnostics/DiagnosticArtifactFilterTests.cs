using System;
using System.Collections.Generic;
using System.Linq;

using NexusLabs.Needlr.Generators.Models;

using Xunit;

using static NexusLabs.Needlr.Generators.Tests.Diagnostics.DiagnosticModelFactory;

namespace NexusLabs.Needlr.Generators.Tests.Diagnostics;

public sealed class DiagnosticArtifactFilterTests
{
    private static readonly DiscoveryResult ServiceDiscovery = new DiagnosticDiscoveryBuilder()
        .WithTypes(
            Type("global::TestApp.OrderService", new[] { "global::TestApp.IOrderService" }),
            Type("global::TestApp.PaymentService", new[] { "global::TestApp.IPaymentService" }))
        .Build();

    [Theory]
    [InlineData("global::TestApp.OrderService")]
    [InlineData("TestApp.OrderService")]
    [InlineData("OrderService")]
    [InlineData("global::TestApp.IOrderService")]
    [InlineData("TestApp.IOrderService")]
    [InlineData("IOrderService")]
    public void ServiceFilter_MatchesEverySupportedIdentity(string term)
    {
        var content = DiagnosticArtifactRenderer.RegistrationIndex(
            ServiceDiscovery,
            DiagnosticArtifactRenderer.Filter(term));

        Assert.Equal(new[] { "OrderService" }, RegisteredImplementations(content));
    }

    [Fact]
    public void ServiceFilter_EmptyFilter_IncludesEveryService()
    {
        var content = DiagnosticArtifactRenderer.RegistrationIndex(
            ServiceDiscovery,
            DiagnosticArtifactRenderer.NoFilter());

        Assert.Equal(new[] { "OrderService", "PaymentService" }, RegisteredImplementations(content));
    }

    [Fact]
    public void ServiceFilter_NonMatchingFilter_ExcludesEveryService()
    {
        var content = DiagnosticArtifactRenderer.RegistrationIndex(
            ServiceDiscovery,
            DiagnosticArtifactRenderer.Filter("TestApp.MissingService"));

        Assert.Empty(RegisteredImplementations(content));
        Assert.Contains("No injectable services discovered.", content);
        Assert.Contains("## Services (0)", content);
    }

    [Fact]
    public void ServiceFilter_MultipleTermsWithSingleMatch_IncludesOnlyMatchingService()
    {
        var content = DiagnosticArtifactRenderer.RegistrationIndex(
            ServiceDiscovery,
            DiagnosticArtifactRenderer.Filter("PaymentService", "TestApp.MissingService"));

        Assert.Equal(new[] { "PaymentService" }, RegisteredImplementations(content));
    }

    [Fact]
    public void ServiceFilter_AppliesToLifetimeSummaryCounts()
    {
        var content = DiagnosticArtifactRenderer.LifetimeSummary(
            ServiceDiscovery,
            DiagnosticArtifactRenderer.Filter("IOrderService"));

        var table = MarkdownDocument.Parse(content).Section("Registration Counts").Tables.Single();

        Assert.Equal("1", table.Cell(0, "Count"));
        Assert.Equal("**1**", table.Cell(3, "Count"));
    }

    [Theory]
    [InlineData("global::TestApp.CachingOrderService")]
    [InlineData("CachingOrderService")]
    [InlineData("global::TestApp.IOrderService")]
    [InlineData("IOrderService")]
    public void DecoratorFilter_MatchesBothRelationshipSides(string term)
    {
        var discovery = new DiagnosticDiscoveryBuilder()
            .WithDecorators(
                Decorator("global::TestApp.CachingOrderService", "global::TestApp.IOrderService"),
                Decorator("global::TestApp.LoggingPaymentService", "global::TestApp.IPaymentService"))
            .Build();

        var content = DiagnosticArtifactRenderer.RegistrationIndex(
            discovery,
            DiagnosticArtifactRenderer.Filter(term));

        var table = MarkdownDocument.Parse(content).SectionStartingWith("Decorators (").Tables.Single();

        Assert.Single(table.Rows);
        Assert.Equal("IOrderService", table.Cell(0, "Service"));
        Assert.Equal("CachingOrderService", table.Cell(0, "Decorator Chain"));
    }

    [Fact]
    public void DecoratorFilter_NonMatchingFilter_OmitsDecoratorSection()
    {
        var discovery = new DiagnosticDiscoveryBuilder()
            .WithDecorators(Decorator("global::TestApp.CachingOrderService", "global::TestApp.IOrderService"))
            .Build();

        var content = DiagnosticArtifactRenderer.RegistrationIndex(
            discovery,
            DiagnosticArtifactRenderer.Filter("TestApp.MissingService"));

        Assert.False(
            MarkdownDocument.Parse(content).HasSectionStartingWith("Decorators ("),
            "Expected the decorators section to be omitted when no decorator matches the filter");
    }

    [Fact]
    public void DecoratorFilter_AppliesToDependencyGraphChains()
    {
        var discovery = new DiagnosticDiscoveryBuilder()
            .WithDecorators(
                Decorator("global::TestApp.CachingOrderService", "global::TestApp.IOrderService"),
                Decorator("global::TestApp.LoggingPaymentService", "global::TestApp.IPaymentService"))
            .Build();

        var content = DiagnosticArtifactRenderer.DependencyGraph(
            discovery,
            DiagnosticArtifactRenderer.Filter("IPaymentService"));

        var block = MarkdownDocument.Parse(content).Section("Decorator Chains").MermaidBlocks.Single();

        Assert.Equal(new[] { "LoggingPaymentService" }, block.Nodes.Select(n => n.Label).ToArray());
    }

    [Theory]
    [InlineData("global::TestApp.WidgetBuilder")]
    [InlineData("WidgetBuilder")]
    [InlineData("global::TestApp.IWidget")]
    [InlineData("IWidget")]
    public void FactoryFilter_MatchesFactoryTypeAndProducedServiceName(string term)
    {
        var discovery = new DiagnosticDiscoveryBuilder()
            .WithFactories(
                Factory("global::TestApp.WidgetBuilder", "global::TestApp.IWidget"),
                Factory("global::TestApp.GadgetBuilder", "global::TestApp.IGadget"))
            .Build();

        var content = DiagnosticArtifactRenderer.RegistrationIndex(
            discovery,
            DiagnosticArtifactRenderer.Filter(term));

        var table = MarkdownDocument.Parse(content).SectionStartingWith("Factories (").Tables.Single();

        Assert.Single(table.Rows);
        Assert.Equal("WidgetBuilder", table.Cell(0, "Source Type"));
    }

    [Fact]
    public void FactoryFilter_AppliesConsistentlyToRegistrationIndexAndDependencyGraph()
    {
        var discovery = new DiagnosticDiscoveryBuilder()
            .WithFactories(
                Factory("global::TestApp.WidgetBuilder"),
                Factory("global::TestApp.GadgetBuilder"))
            .Build();

        var filter = DiagnosticArtifactRenderer.Filter("WidgetBuilder");
        var registrationIndex = MarkdownDocument.Parse(DiagnosticArtifactRenderer.RegistrationIndex(discovery, filter));
        var dependencyGraph = MarkdownDocument.Parse(DiagnosticArtifactRenderer.DependencyGraph(discovery, filter));

        Assert.Equal(
            new[] { "WidgetBuilder" },
            registrationIndex.SectionStartingWith("Factories (").Tables.Single().Column("Source Type"));
        Assert.Equal(
            new[] { "WidgetBuilderFactory", "WidgetBuilder" },
            dependencyGraph.Section("Factory Services").MermaidBlocks.Single().Nodes.Select(n => n.Label).ToArray());
    }

    [Theory]
    [InlineData("global::TestApp.DatabaseOptions")]
    [InlineData("DatabaseOptions")]
    [InlineData("Database")]
    [InlineData("Primary")]
    public void OptionsFilter_MatchesTypeSectionAndNameIdentities(string term)
    {
        var discovery = new DiagnosticDiscoveryBuilder()
            .WithOptions(
                NamedOptions("global::TestApp.DatabaseOptions", "Database", "Primary"),
                OptionsType("global::TestApp.CacheOptions", "Cache"))
            .Build();

        var content = DiagnosticArtifactRenderer.OptionsSummary(
            discovery,
            DiagnosticArtifactRenderer.Filter(term));

        var table = MarkdownDocument.Parse(content).Section("Options Classes").Tables.Single();

        Assert.Single(table.Rows);
        Assert.Equal("`DatabaseOptions`", table.Cell(0, "Class"));
    }

    [Fact]
    public void OptionsFilter_NonMatchingFilter_ReportsNoDiscoveredOptions()
    {
        var discovery = new DiagnosticDiscoveryBuilder()
            .WithOptions(OptionsType("global::TestApp.CacheOptions", "Cache"))
            .Build();

        var content = DiagnosticArtifactRenderer.OptionsSummary(
            discovery,
            DiagnosticArtifactRenderer.Filter("Database"));

        Assert.Contains("*No options classes discovered.", content);
        Assert.False(
            MarkdownDocument.Parse(content).HasSection("Options Classes"),
            "Expected no options table when the filter matches nothing");
    }

    [Theory]
    [InlineData("global::TestApp.OrderService")]
    [InlineData("OrderService")]
    [InlineData("global::TestApp.IOrderService")]
    [InlineData("global::TestApp.AuditInterceptor")]
    [InlineData("AuditInterceptor")]
    public void InterceptorFilter_MatchesServiceAndInterceptorIdentities(string term)
    {
        var discovery = new DiagnosticDiscoveryBuilder()
            .WithInterceptedServices(
                Intercepted(
                    "global::TestApp.OrderService",
                    new[] { "global::TestApp.IOrderService" },
                    new[] { "global::TestApp.AuditInterceptor" }),
                Intercepted("global::TestApp.PaymentService", new[] { "global::TestApp.RetryInterceptor" }))
            .Build();

        var content = DiagnosticArtifactRenderer.RegistrationIndex(
            discovery,
            DiagnosticArtifactRenderer.Filter(term));

        var table = MarkdownDocument.Parse(content).SectionStartingWith("Intercepted Services (").Tables.Single();

        Assert.Single(table.Rows);
        Assert.Equal("OrderService", table.Cell(0, "Service"));
        Assert.Equal("AuditInterceptor", table.Cell(0, "Interceptors"));
    }

    [Fact]
    public void PluginFilter_MatchesPluginTypeName()
    {
        var discovery = new DiagnosticDiscoveryBuilder()
            .WithPlugins(
                Plugin("global::TestApp.OrderPlugin"),
                Plugin("global::TestApp.PaymentPlugin"))
            .Build();

        var content = DiagnosticArtifactRenderer.RegistrationIndex(
            discovery,
            DiagnosticArtifactRenderer.Filter("TestApp.OrderPlugin"));

        var table = MarkdownDocument.Parse(content).SectionStartingWith("Plugins (").Tables.Single();

        Assert.Single(table.Rows);
        Assert.Equal("OrderPlugin", table.Cell(0, "Plugin"));
    }

    [Theory]
    [InlineData("global::Plugin.CachingOrderService")]
    [InlineData("CachingOrderService")]
    [InlineData("global::Plugin.IOrderService")]
    public void PluginProvidedTypeFilter_MatchesReferencedAssemblyIdentities(string term)
    {
        var referenced = DiagnosticArtifactRenderer.ReferencedAssembly(
            "Plugin.Assembly",
            PluginType(
                "global::Plugin.CachingOrderService",
                "CachingOrderService",
                new[] { "global::Plugin.IOrderService" },
                isDecorator: true,
                hasFactory: true,
                hasInterceptorProxy: true),
            PluginType("global::Plugin.UnrelatedService", "UnrelatedService"));

        var content = DiagnosticArtifactRenderer.RegistrationIndex(
            new DiagnosticDiscoveryBuilder().Build(),
            DiagnosticArtifactRenderer.Filter(term),
            referenced);

        var document = MarkdownDocument.Parse(content);
        var table = document.SectionStartingWith("Plugin.Assembly (").Tables.Single();

        Assert.Single(table.Rows);
        Assert.Equal("CachingOrderService", table.Cell(0, "Implementation"));
        Assert.Equal(
            new[] { "CachingOrderService" },
            document.SectionStartingWith("Decorators (").Tables.Single().Column("Decorator Chain"));
        Assert.Equal(
            new[] { "CachingOrderService" },
            document.SectionStartingWith("Factories (").Tables.Single().Column("Source Type"));
        Assert.Equal(
            new[] { "CachingOrderService" },
            document.SectionStartingWith("Intercepted Services (").Tables.Single().Column("Service"));
    }

    [Fact]
    public void PluginProvidedTypeFilter_NonMatchingFilter_OmitsReferencedAssemblySections()
    {
        var referenced = DiagnosticArtifactRenderer.ReferencedAssembly(
            "Plugin.Assembly",
            PluginType(
                "global::Plugin.CachingOrderService",
                "CachingOrderService",
                Array.Empty<string>(),
                isDecorator: true,
                hasFactory: true,
                hasInterceptorProxy: true));

        var filter = DiagnosticArtifactRenderer.Filter("TestApp.MissingService");
        var discovery = new DiagnosticDiscoveryBuilder().Build();

        var registrationIndex = MarkdownDocument.Parse(
            DiagnosticArtifactRenderer.RegistrationIndex(discovery, filter, referenced));
        var dependencyGraph = MarkdownDocument.Parse(
            DiagnosticArtifactRenderer.DependencyGraph(discovery, filter, referenced));
        var lifetimeSummary = MarkdownDocument.Parse(
            DiagnosticArtifactRenderer.LifetimeSummary(discovery, filter, referenced));

        Assert.False(
            registrationIndex.HasSection("Referenced Plugin Assemblies"),
            "Expected the registration index to omit referenced assemblies filtered out entirely");
        Assert.False(
            dependencyGraph.HasSection("Referenced Plugin Assemblies"),
            "Expected the dependency graph to omit referenced assemblies filtered out entirely");
        Assert.False(
            lifetimeSummary.HasSection("Referenced Plugin Assemblies"),
            "Expected the lifetime summary to omit referenced assemblies filtered out entirely");
    }

    private static IReadOnlyList<string> RegisteredImplementations(string content)
    {
        var section = MarkdownDocument.Parse(content).SectionStartingWith("Services (");
        return section.Tables.Count == 0
            ? Array.Empty<string>()
            : section.Tables[0].Column("Implementation");
    }
}
