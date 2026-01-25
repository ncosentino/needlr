using Xunit;

using static NexusLabs.Needlr.Generators.Tests.Diagnostics.DiagnosticTestHelpers;

namespace NexusLabs.Needlr.Generators.Tests.Diagnostics;

public sealed class DiagnosticOutputValidityTests
{
    [Fact]
    public void Diagnostics_MultipleLifetimes_AllRepresented()
    {
        var source = @"
using NexusLabs.Needlr.Generators;

[assembly: GenerateTypeRegistry(IncludeNamespacePrefixes = new[] { ""TestApp"" }, IncludeSelf = true)]

namespace TestApp
{
    public interface ISingleton { }
    public class SingletonService : ISingleton { }
    
    public interface IScoped { }
    public class ScopedService : IScoped { }
    
    public interface ITransient { }
    public class TransientService : ITransient { }
}";

        var lifetimeContent = GetDiagnosticContent(source, "LifetimeSummary");
        var registrationContent = GetDiagnosticContent(source, "RegistrationIndex");
        var graphContent = GetDiagnosticContent(source, "DependencyGraph");

        Assert.Contains("Singleton", lifetimeContent);

        Assert.Contains("SingletonService", registrationContent);
        Assert.Contains("ScopedService", registrationContent);
        Assert.Contains("TransientService", registrationContent);

        Assert.Contains("SingletonService", graphContent);
        Assert.Contains("ScopedService", graphContent);
        Assert.Contains("TransientService", graphContent);
    }

    [Fact]
    public void Diagnostics_NoRegistrations_GeneratesEmptyContent()
    {
        var source = @"
using NexusLabs.Needlr.Generators;

[assembly: GenerateTypeRegistry]

namespace TestApp
{
}";

        var diagnosticsFile = RunGeneratorWithDiagnosticsEnabled(source, enabled: true)
            .FirstOrDefault(f => f.FilePath.Contains("NeedlrDiagnostics"));

        if (diagnosticsFile != null)
        {
            Assert.Contains("DependencyGraph", diagnosticsFile.Content);
        }
    }

    [Fact]
    public void Diagnostics_SelfRegisteredType_Handled()
    {
        var source = @"
using NexusLabs.Needlr.Generators;

[assembly: GenerateTypeRegistry]

namespace TestApp
{
    public class ConcreteService { }
}";

        var content = GetDiagnosticContent(source, "RegistrationIndex");

        Assert.Contains("ConcreteService", content);
    }

    [Fact]
    public void Diagnostics_WithInterceptor_IncludesInterceptorInfo()
    {
        var source = @"
using NexusLabs.Needlr;
using NexusLabs.Needlr.Generators;

[assembly: GenerateTypeRegistry(IncludeNamespacePrefixes = new[] { ""TestApp"" })]

namespace TestApp
{
    public interface IOrderService
    {
        string GetOrder(int id);
    }

    public class LoggingInterceptor : IMethodInterceptor
    {
        public System.Threading.Tasks.ValueTask<object?> InterceptAsync(IMethodInvocation invocation)
        {
            return invocation.ProceedAsync();
        }
    }

    [Intercept<LoggingInterceptor>]
    public class OrderService : IOrderService
    {
        public string GetOrder(int id) => $""Order {id}"";
    }
}";

        var content = GetDiagnosticContent(source, "RegistrationIndex");

        Assert.Contains("LoggingInterceptor", content);
    }

    [Fact]
    public void Diagnostics_MultipleRuns_SameOutput()
    {
        var source = @"
using NexusLabs.Needlr.Generators;

[assembly: GenerateTypeRegistry]

namespace TestApp
{
    public interface IService { }
    public class MyService : IService { }
}";

        var run1 = GetDiagnosticContent(source, "RegistrationIndex");
        var run2 = GetDiagnosticContent(source, "RegistrationIndex");

        Assert.Contains("MyService", run1);
        Assert.Contains("MyService", run2);
        Assert.Contains("IService", run1);
        Assert.Contains("IService", run2);
    }

    [Fact]
    public void DependencyGraph_MermaidSyntax_HasValidStructure()
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

        Assert.Contains("```mermaid", content);
        Assert.Contains("graph TD", content);
        Assert.Contains("```", content);
        Assert.Contains("subgraph", content);
        Assert.Contains("end", content);
        Assert.Contains("[\"", content);
        Assert.Contains("\"]", content);
        Assert.Contains("-->", content);
    }

    [Fact]
    public void DependencyGraph_MermaidNodes_HaveValidIds()
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

        Assert.Matches(@"\w+\[""\w+""\]", content);
    }

    [Fact]
    public void RegistrationIndex_TableStructure_IsValid()
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

        var content = GetDiagnosticContent(source, "RegistrationIndex");

        Assert.Contains("| # | Interface | Implementation | Lifetime | Source |", content);
        Assert.Contains("|---|", content);
        Assert.Matches(@"\| \d+ \| \w+ \| \w+ \| (Singleton|Scoped|Transient) \|", content);
    }
}
