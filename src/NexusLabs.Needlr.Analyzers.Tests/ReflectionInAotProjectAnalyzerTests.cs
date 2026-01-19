using System.Threading.Tasks;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

using Xunit;

namespace NexusLabs.Needlr.Analyzers.Tests;

public sealed class ReflectionInAotProjectAnalyzerTests
{
    [Fact]
    public async Task NoWarning_WhenNotAotProject()
    {
        var code = @"
using NexusLabs.Needlr.Injection.Reflection;

class Program
{
    void Method()
    {
        var registrar = new ReflectionTypeRegistrar();
    }
}

namespace NexusLabs.Needlr.Injection.Reflection
{
    public class ReflectionTypeRegistrar { }
}
";

        var test = new CSharpAnalyzerTest<ReflectionInAotProjectAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        // No AOT properties set, so no diagnostic expected
        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Error_WhenReflectionTypeInstantiatedInAotProject()
    {
        var code = @"
using NexusLabs.Needlr.Injection.Reflection;

class Program
{
    void Method()
    {
        var registrar = new {|#0:ReflectionTypeRegistrar|}();
    }
}

namespace NexusLabs.Needlr.Injection.Reflection
{
    public class ReflectionTypeRegistrar { }
}
";

        var expected = new DiagnosticResult(DiagnosticIds.ReflectionInAotProject, DiagnosticSeverity.Error)
            .WithLocation(0)
            .WithArguments("ReflectionTypeRegistrar");

        var test = new CSharpAnalyzerTest<ReflectionInAotProjectAnalyzer, DefaultVerifier>
        {
            TestCode = code,
            ExpectedDiagnostics = { expected }
        };

        // Simulate AOT project
        test.TestState.AnalyzerConfigFiles.Add(("/.globalconfig", @"
is_global = true
build_property.PublishAot = true
"));

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Error_WhenReflectionMethodCalledInAotProject()
    {
        var code = @"
class Program
{
    void Method()
    {
        var syringe = new Syringe();
        syringe.{|#0:UsingReflectionTypeRegistrar|}();
    }
}

class Syringe
{
    public Syringe UsingReflectionTypeRegistrar() => this;
}
";

        var expected = new DiagnosticResult(DiagnosticIds.ReflectionInAotProject, DiagnosticSeverity.Error)
            .WithLocation(0)
            .WithArguments("UsingReflectionTypeRegistrar");

        var test = new CSharpAnalyzerTest<ReflectionInAotProjectAnalyzer, DefaultVerifier>
        {
            TestCode = code,
            ExpectedDiagnostics = { expected }
        };

        test.TestState.AnalyzerConfigFiles.Add(("/.globalconfig", @"
is_global = true
build_property.IsAotCompatible = true
"));

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Error_WhenReflectionPluginFactoryUsedInAotProject()
    {
        var code = @"
using NexusLabs.Needlr.Injection.Reflection;

class Program
{
    void Method()
    {
        var factory = new {|#0:ReflectionPluginFactory|}();
    }
}

namespace NexusLabs.Needlr.Injection.Reflection
{
    public class ReflectionPluginFactory { }
}
";

        var expected = new DiagnosticResult(DiagnosticIds.ReflectionInAotProject, DiagnosticSeverity.Error)
            .WithLocation(0)
            .WithArguments("ReflectionPluginFactory");

        var test = new CSharpAnalyzerTest<ReflectionInAotProjectAnalyzer, DefaultVerifier>
        {
            TestCode = code,
            ExpectedDiagnostics = { expected }
        };

        test.TestState.AnalyzerConfigFiles.Add(("/.globalconfig", @"
is_global = true
build_property.PublishAot = true
"));

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Error_WhenMultipleReflectionApisUsedInAotProject()
    {
        var code = @"
class Program
{
    void Method()
    {
        var syringe = new Syringe();
        syringe.{|#0:UsingReflectionTypeRegistrar|}()
               .{|#1:UsingReflectionTypeFilterer|}();
    }
}

class Syringe
{
    public Syringe UsingReflectionTypeRegistrar() => this;
    public Syringe UsingReflectionTypeFilterer() => this;
}
";

        var expected1 = new DiagnosticResult(DiagnosticIds.ReflectionInAotProject, DiagnosticSeverity.Error)
            .WithLocation(0)
            .WithArguments("UsingReflectionTypeRegistrar");
        var expected2 = new DiagnosticResult(DiagnosticIds.ReflectionInAotProject, DiagnosticSeverity.Error)
            .WithLocation(1)
            .WithArguments("UsingReflectionTypeFilterer");

        var test = new CSharpAnalyzerTest<ReflectionInAotProjectAnalyzer, DefaultVerifier>
        {
            TestCode = code,
            ExpectedDiagnostics = { expected1, expected2 }
        };

        test.TestState.AnalyzerConfigFiles.Add(("/.globalconfig", @"
is_global = true
build_property.PublishAot = true
"));

        await test.RunAsync(TestContext.Current.CancellationToken);
    }
}
