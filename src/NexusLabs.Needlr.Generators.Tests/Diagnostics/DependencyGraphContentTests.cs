using Xunit;

using static NexusLabs.Needlr.Generators.Tests.Diagnostics.DiagnosticTestHelpers;

namespace NexusLabs.Needlr.Generators.Tests.Diagnostics;

public sealed class DependencyGraphContentTests
{
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
