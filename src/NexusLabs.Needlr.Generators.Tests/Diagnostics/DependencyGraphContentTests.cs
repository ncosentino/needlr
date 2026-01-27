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
}
