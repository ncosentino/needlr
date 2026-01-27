using Xunit;

using static NexusLabs.Needlr.Generators.Tests.Diagnostics.DiagnosticTestHelpers;

namespace NexusLabs.Needlr.Generators.Tests.Diagnostics;

public sealed class DependencyGraphContentTests
{
    #region Decorator Chain Tests

    [Fact]
    public void DependencyGraph_ShowsDecoratorChainsSection()
    {
        var source = @"
using NexusLabs.Needlr;
using NexusLabs.Needlr.Generators;

[assembly: GenerateTypeRegistry]

namespace TestApp
{
    public interface IOrderService { }
    public class OrderService : IOrderService { }

    [DecoratorFor<IOrderService>(Order = 1)]
    public class LoggingDecorator : IOrderService
    {
        public LoggingDecorator(IOrderService inner) { }
    }

    [DecoratorFor<IOrderService>(Order = 2)]
    public class CachingDecorator : IOrderService
    {
        public CachingDecorator(IOrderService inner) { }
    }
}";

        var content = GetDiagnosticContent(source, "DependencyGraph");

        Assert.Contains("## Decorator Chains", content);
        Assert.Contains("graph LR", content);
    }

    [Fact]
    public void DependencyGraph_ShowsDecoratorChainOrder()
    {
        var source = @"
using NexusLabs.Needlr;
using NexusLabs.Needlr.Generators;

[assembly: GenerateTypeRegistry]

namespace TestApp
{
    public interface IOrderService { }
    public class OrderService : IOrderService { }

    [DecoratorFor<IOrderService>(Order = 1)]
    public class LoggingDecorator : IOrderService
    {
        public LoggingDecorator(IOrderService inner) { }
    }

    [DecoratorFor<IOrderService>(Order = 2)]
    public class CachingDecorator : IOrderService
    {
        public CachingDecorator(IOrderService inner) { }
    }
}";

        var content = GetDiagnosticContent(source, "DependencyGraph");

        // Should show CachingDecorator (order 2) -> LoggingDecorator (order 1) -> OrderService
        Assert.Contains("CachingDecorator", content);
        Assert.Contains("LoggingDecorator", content);
        Assert.Contains("OrderService", content);
        // Chain arrows should be present
        Assert.Contains("-->", content);
    }

    #endregion

    #region Keyed Service Cluster Tests

    [Fact]
    public void DependencyGraph_ShowsKeyedServicesSection()
    {
        var source = @"
using NexusLabs.Needlr;
using NexusLabs.Needlr.Generators;

[assembly: GenerateTypeRegistry]

namespace TestApp
{
    public interface ICache { }

    [Keyed(""redis"")]
    public class RedisCache : ICache { }

    [Keyed(""memory"")]
    public class MemoryCache : ICache { }
}";

        var content = GetDiagnosticContent(source, "DependencyGraph");

        Assert.Contains("## Keyed Services", content);
    }

    [Fact]
    public void DependencyGraph_GroupsKeyedServicesByKey()
    {
        var source = @"
using NexusLabs.Needlr;
using NexusLabs.Needlr.Generators;

[assembly: GenerateTypeRegistry]

namespace TestApp
{
    public interface ICache { }

    [Keyed(""redis"")]
    public class RedisCache : ICache { }

    [Keyed(""redis"")]
    public class RedisDistributedCache : ICache { }

    [Keyed(""memory"")]
    public class MemoryCache : ICache { }
}";

        var content = GetDiagnosticContent(source, "DependencyGraph");

        Assert.Contains("redis", content);
        Assert.Contains("memory", content);
        Assert.Contains("RedisCache", content);
        Assert.Contains("MemoryCache", content);
    }

    #endregion

    #region Plugin Assembly Boundary Tests

    [Fact]
    public void DependencyGraph_ShowsPluginAssembliesSection()
    {
        var source = @"
using NexusLabs.Needlr;
using NexusLabs.Needlr.Generators;

[assembly: GenerateTypeRegistry]

namespace TestApp
{
    public interface IPlugin : IConfigureRegistrations { }
    public class MyPlugin : IPlugin 
    {
        public void ConfigureRegistrations(IRegistrationContext context) { }
    }
}";

        var content = GetDiagnosticContent(source, "DependencyGraph");

        Assert.Contains("## Plugin Assemblies", content);
    }

    [Fact]
    public void DependencyGraph_GroupsPluginsByAssembly()
    {
        var source = @"
using NexusLabs.Needlr;
using NexusLabs.Needlr.Generators;

[assembly: GenerateTypeRegistry]

namespace TestApp
{
    public interface IPlugin : IConfigureRegistrations { }
    public class OrderPlugin : IPlugin 
    {
        public void ConfigureRegistrations(IRegistrationContext context) { }
    }
    public class PaymentPlugin : IPlugin 
    {
        public void ConfigureRegistrations(IRegistrationContext context) { }
    }
}";

        var content = GetDiagnosticContent(source, "DependencyGraph");

        Assert.Contains("OrderPlugin", content);
        Assert.Contains("PaymentPlugin", content);
    }

    #endregion

    #region Factory-Generated Services Tests

    [Fact]
    public void DependencyGraph_ShowsFactoriesSection()
    {
        var source = @"
using NexusLabs.Needlr.Generators;
using NexusLabs.Needlr.Generators.Attributes;

[assembly: GenerateTypeRegistry]

namespace TestApp
{
    public interface IConnection { }
    
    [GenerateFactory]
    public class Connection : IConnection
    {
        public Connection(string connectionString) { }
    }
}";

        var content = GetDiagnosticContent(source, "DependencyGraph");

        Assert.Contains("## Factory Services", content);
    }

    [Fact]
    public void DependencyGraph_ShowsFactoryWithHexagonShape()
    {
        var source = @"
using NexusLabs.Needlr.Generators;
using NexusLabs.Needlr.Generators.Attributes;

[assembly: GenerateTypeRegistry]

namespace TestApp
{
    public interface IConnection { }
    
    [GenerateFactory]
    public class Connection : IConnection
    {
        public Connection(string connectionString) { }
    }
}";

        var content = GetDiagnosticContent(source, "DependencyGraph");

        // Source type (product) shown with hexagon shape
        Assert.Contains("{{\"Connection\"}}", content);
    }

    [Fact]
    public void DependencyGraph_ShowsFactoryProducesRelationship()
    {
        var source = @"
using NexusLabs.Needlr.Generators;
using NexusLabs.Needlr.Generators.Attributes;

[assembly: GenerateTypeRegistry]

namespace TestApp
{
    public interface IConnection { }
    
    [GenerateFactory]
    public class Connection : IConnection
    {
        public Connection(string connectionString) { }
    }
}";

        var content = GetDiagnosticContent(source, "DependencyGraph");

        // Shows the generated factory with regular shape
        Assert.Contains("ConnectionFactory[\"ConnectionFactory\"]", content);
        // Shows produces relationship
        Assert.Contains("-.->|produces|", content);
    }

    #endregion

    #region Interface Implementation Mapping Tests

    [Fact]
    public void DependencyGraph_ShowsInterfaceMappingSection()
    {
        var source = @"
using NexusLabs.Needlr.Generators;

[assembly: GenerateTypeRegistry]

namespace TestApp
{
    public interface IOrderService { }
    public class OrderService : IOrderService { }

    public interface IPaymentService { }
    public class PaymentService : IPaymentService { }
}";

        var content = GetDiagnosticContent(source, "DependencyGraph");

        Assert.Contains("## Interface Mapping", content);
    }

    [Fact]
    public void DependencyGraph_ShowsDottedInterfaceEdges()
    {
        var source = @"
using NexusLabs.Needlr.Generators;

[assembly: GenerateTypeRegistry]

namespace TestApp
{
    public interface IOrderService { }
    public class OrderService : IOrderService { }
}";

        var content = GetDiagnosticContent(source, "DependencyGraph");

        // Dotted edges use -.->
        Assert.Contains("IOrderService", content);
        Assert.Contains("OrderService", content);
        Assert.Contains("-.->", content);
    }

    #endregion

    #region Complexity Metrics Tests

    [Fact]
    public void DependencyGraph_ShowsComplexityMetricsSection()
    {
        var source = @"
using NexusLabs.Needlr.Generators;

[assembly: GenerateTypeRegistry]

namespace TestApp
{
    public interface IOrderService { }
    public class OrderService : IOrderService { }
}";

        var content = GetDiagnosticContent(source, "DependencyGraph");

        Assert.Contains("## Complexity Metrics", content);
    }

    [Fact]
    public void DependencyGraph_ShowsMaxDepth()
    {
        var source = @"
using NexusLabs.Needlr.Generators;

[assembly: GenerateTypeRegistry]

namespace TestApp
{
    public interface ILogger { }
    public class Logger : ILogger { }

    public interface IRepository { }
    public class Repository : IRepository
    {
        public Repository(ILogger logger) { }
    }

    public interface IOrderService { }
    public class OrderService : IOrderService
    {
        public OrderService(IRepository repo) { }
    }
}";

        var content = GetDiagnosticContent(source, "DependencyGraph");

        Assert.Contains("Max Dependency Depth", content);
    }

    [Fact]
    public void DependencyGraph_ShowsHubServices()
    {
        var source = @"
using NexusLabs.Needlr.Generators;

[assembly: GenerateTypeRegistry]

namespace TestApp
{
    public interface ILogger { }
    public class Logger : ILogger { }

    public interface IService1 { }
    public class Service1 : IService1 { public Service1(ILogger logger) { } }

    public interface IService2 { }
    public class Service2 : IService2 { public Service2(ILogger logger) { } }

    public interface IService3 { }
    public class Service3 : IService3 { public Service3(ILogger logger) { } }

    public interface IService4 { }
    public class Service4 : IService4 { public Service4(ILogger logger) { } }
}";

        var content = GetDiagnosticContent(source, "DependencyGraph");

        Assert.Contains("Hub Services", content);
    }

    #endregion

    [Fact]
    public void DependencyGraph_ContainsMermaidHeader()
    {
        var source = @"
using NexusLabs.Needlr.Generators;

[assembly: GenerateTypeRegistry]

namespace TestApp
{
    public interface IMyService { }
    public class MyService : IMyService { }
}";

        var content = GetDiagnosticContent(source, "DependencyGraph");

        Assert.Contains("# Needlr Dependency Graph", content);
        Assert.Contains("```mermaid", content);
        Assert.Contains("graph TD", content);
        Assert.Contains("```", content);
    }

    [Fact]
    public void DependencyGraph_IncludesServiceRegistrations()
    {
        var source = @"
using NexusLabs.Needlr.Generators;

[assembly: GenerateTypeRegistry]

namespace TestApp
{
    public interface IOrderService { }
    public class OrderService : IOrderService { }
}";

        var content = GetDiagnosticContent(source, "DependencyGraph");

        Assert.Contains("OrderService", content);
    }

    [Fact]
    public void DependencyGraph_ShowsLifetimeSubgraphs()
    {
        var source = @"
using NexusLabs.Needlr.Generators;

[assembly: GenerateTypeRegistry(IncludeNamespacePrefixes = new[] { ""TestApp"" }, IncludeSelf = true)]

namespace TestApp
{
    public interface ISingletonService { }
    public class SingletonService : ISingletonService { }
    
    public interface IScopedService { }
    public class ScopedService : IScopedService { }
    
    public interface ITransientService { }
    public class TransientService : ITransientService { }
}";

        var content = GetDiagnosticContent(source, "DependencyGraph");

        Assert.Contains("Singleton", content);
    }

    [Fact]
    public void DependencyGraph_ShowsDependencyEdges()
    {
        var source = @"
using NexusLabs.Needlr.Generators;

[assembly: GenerateTypeRegistry]

namespace TestApp
{
    public interface ILogger { }
    public class Logger : ILogger { }

    public interface IOrderService { }
    public class OrderService : IOrderService
    {
        public OrderService(ILogger logger) { }
    }
}";

        var content = GetDiagnosticContent(source, "DependencyGraph");

        Assert.Contains("OrderService", content);
        Assert.Contains("ILogger", content);
        Assert.Contains("-->", content);
    }

    #region Referenced TypeRegistry Assemblies Tests

    [Fact]
    public void DependencyGraph_ShowsReferencedTypeRegistryAssembliesSection()
    {
        // This test verifies that when a host assembly references a plugin assembly
        // with [GenerateTypeRegistry], the diagnostics show that reference
        var content = GetDiagnosticContentWithReferencedAssembly(
            hostSource: @"
using NexusLabs.Needlr.Generators;

[assembly: GenerateTypeRegistry]

namespace HostApp
{
    public interface IHostService { }
    public class HostService : IHostService { }
}",
            pluginSource: @"
using NexusLabs.Needlr.Generators;

[assembly: GenerateTypeRegistry]

namespace PluginLib
{
    public interface IPluginService { }
    public class PluginService : IPluginService { }
}",
            pluginAssemblyName: "MyPlugin",
            fieldName: "DependencyGraph");

        Assert.Contains("## Referenced Plugin Assemblies", content);
        Assert.Contains("MyPlugin", content);
    }

    [Fact]
    public void DependencyGraph_ShowsMultipleReferencedAssemblies()
    {
        var content = GetDiagnosticContentWithMultipleReferencedAssemblies(
            hostSource: @"
using NexusLabs.Needlr.Generators;

[assembly: GenerateTypeRegistry]

namespace HostApp
{
    public interface IHostService { }
    public class HostService : IHostService { }
}",
            pluginSources: new[]
            {
                (@"
using NexusLabs.Needlr.Generators;

[assembly: GenerateTypeRegistry]

namespace PluginA
{
    public interface IServiceA { }
    public class ServiceA : IServiceA { }
}", "PluginAssemblyA"),
                (@"
using NexusLabs.Needlr.Generators;

[assembly: GenerateTypeRegistry]

namespace PluginB
{
    public interface IServiceB { }
    public class ServiceB : IServiceB { }
}", "PluginAssemblyB")
            },
            fieldName: "DependencyGraph");

        Assert.Contains("## Referenced Plugin Assemblies", content);
        Assert.Contains("PluginAssemblyA", content);
        Assert.Contains("PluginAssemblyB", content);
    }

    [Fact]
    public void DependencyGraph_OmitsReferencedAssembliesSectionWhenNone()
    {
        // When there are no referenced assemblies with [GenerateTypeRegistry],
        // the section should not appear
        var source = @"
using NexusLabs.Needlr.Generators;

[assembly: GenerateTypeRegistry]

namespace TestApp
{
    public interface IMyService { }
    public class MyService : IMyService { }
}";

        var content = GetDiagnosticContent(source, "DependencyGraph");

        Assert.DoesNotContain("## Referenced Plugin Assemblies", content);
    }

    [Fact]
    public void DependencyGraph_ReferencedAssembliesShowsPluginTypes()
    {
        var content = GetDiagnosticContentWithReferencedAssembly(
            hostSource: @"
using NexusLabs.Needlr.Generators;

[assembly: GenerateTypeRegistry]

namespace HostApp
{
    public interface IHostService { }
    public class HostService : IHostService { }
}",
            pluginSource: @"
using NexusLabs.Needlr.Generators;

[assembly: GenerateTypeRegistry]

namespace PluginLib
{
    public interface IPluginService { }
    public class PluginService : IPluginService { }
}",
            pluginAssemblyName: "MyPlugin",
            fieldName: "DependencyGraph");

        // Should show plugin types in the referenced assembly section
        Assert.Contains("PluginService", content);
        Assert.Contains("IPluginService", content);
    }

    [Fact]
    public void LifetimeSummary_ShowsReferencedPluginAssembliesSection()
    {
        var content = GetDiagnosticContentWithReferencedAssembly(
            hostSource: @"
using NexusLabs.Needlr.Generators;

[assembly: GenerateTypeRegistry]

namespace HostApp
{
    public interface IHostService { }
    public class HostService : IHostService { }
}",
            pluginSource: @"
using NexusLabs.Needlr.Generators;

[assembly: GenerateTypeRegistry]

namespace PluginLib
{
    public interface IPluginService { }
    public class PluginService : IPluginService { }
}",
            pluginAssemblyName: "MyPlugin",
            fieldName: "LifetimeSummary");

        Assert.Contains("## Referenced Plugin Assemblies", content);
        Assert.Contains("MyPlugin", content);
        Assert.Contains("Singleton", content);
    }

    [Fact]
    public void RegistrationIndex_ShowsReferencedPluginAssembliesSection()
    {
        var content = GetDiagnosticContentWithReferencedAssembly(
            hostSource: @"
using NexusLabs.Needlr.Generators;

[assembly: GenerateTypeRegistry]

namespace HostApp
{
    public interface IHostService { }
    public class HostService : IHostService { }
}",
            pluginSource: @"
using NexusLabs.Needlr.Generators;

[assembly: GenerateTypeRegistry]

namespace PluginLib
{
    public interface IPluginService { }
    public class PluginService : IPluginService { }
}",
            pluginAssemblyName: "MyPlugin",
            fieldName: "RegistrationIndex");

        Assert.Contains("## Referenced Plugin Assemblies", content);
        Assert.Contains("MyPlugin", content);
        Assert.Contains("PluginService", content);
        Assert.Contains("IPluginService", content);
    }

    [Fact]
    public void RegistrationIndex_ShowsPluginTypeCount()
    {
        var content = GetDiagnosticContentWithReferencedAssembly(
            hostSource: @"
using NexusLabs.Needlr.Generators;

[assembly: GenerateTypeRegistry]

namespace HostApp
{
    public interface IHostService { }
    public class HostService : IHostService { }
}",
            pluginSource: @"
using NexusLabs.Needlr.Generators;

[assembly: GenerateTypeRegistry]

namespace PluginLib
{
    public interface IPluginService { }
    public class PluginService : IPluginService { }
}",
            pluginAssemblyName: "MyPlugin",
            fieldName: "RegistrationIndex");

        // Should show the service count in the header
        Assert.Contains("MyPlugin (1 services)", content);
    }

    [Fact]
    public void LifetimeSummary_OmitsPluginSectionWhenNone()
    {
        var source = @"
using NexusLabs.Needlr.Generators;

[assembly: GenerateTypeRegistry]

namespace TestApp
{
    public interface IMyService { }
    public class MyService : IMyService { }
}";

        var content = GetDiagnosticContent(source, "LifetimeSummary");

        Assert.DoesNotContain("## Referenced Plugin Assemblies", content);
    }

    [Fact]
    public void RegistrationIndex_OmitsPluginSectionWhenNone()
    {
        var source = @"
using NexusLabs.Needlr.Generators;

[assembly: GenerateTypeRegistry]

namespace TestApp
{
    public interface IMyService { }
    public class MyService : IMyService { }
}";

        var content = GetDiagnosticContent(source, "RegistrationIndex");

        Assert.DoesNotContain("## Referenced Plugin Assemblies", content);
    }

    [Fact]
    public void DependencyGraph_ReferencedAssembliesShowsFactoryProducesEdge()
    {
        var content = GetDiagnosticContentWithReferencedAssembly(
            hostSource: @"
using NexusLabs.Needlr.Generators;

[assembly: GenerateTypeRegistry]

namespace HostApp
{
    public interface IHostService { }
    public class HostService : IHostService { }
}",
            pluginSource: @"
using NexusLabs.Needlr.Generators;

[assembly: GenerateTypeRegistry]

namespace PluginLib
{
    public interface IConnection { }
    
    [GenerateFactory]
    public class Connection : IConnection
    {
        public Connection(string connectionString) { }
    }
}",
            pluginAssemblyName: "MyPlugin",
            fieldName: "DependencyGraph");

        // Should show factory→product relationship in Referenced Plugin Assemblies section
        Assert.Contains("-.->|produces|", content);
    }

    [Fact]
    public void DependencyGraph_ConsolidatedFactorySectionIncludesPluginFactories()
    {
        var content = GetDiagnosticContentWithReferencedAssembly(
            hostSource: @"
using NexusLabs.Needlr.Generators;

[assembly: GenerateTypeRegistry]

namespace HostApp
{
    public interface IHostService { }
    public class HostService : IHostService { }
}",
            pluginSource: @"
using NexusLabs.Needlr.Generators;

[assembly: GenerateTypeRegistry]

namespace PluginLib
{
    public interface IConnection { }
    
    [GenerateFactory]
    public class Connection : IConnection
    {
        public Connection(string connectionString) { }
    }
}",
            pluginAssemblyName: "MyPlugin",
            fieldName: "DependencyGraph");

        // Should show consolidated Factory Services section with plugin factory
        Assert.Contains("## Factory Services", content);
        Assert.Contains("ConnectionFactory", content);
    }

    #endregion
}
