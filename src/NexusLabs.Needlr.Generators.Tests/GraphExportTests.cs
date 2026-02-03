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
}
