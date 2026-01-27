using System.Threading.Tasks;

using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

using Xunit;

namespace NexusLabs.Needlr.Generators.Tests;

/// <summary>
/// Tests for OpenDecoratorForAttributeAnalyzer diagnostics:
/// - NDLRGEN006: Type must be an open generic interface
/// - NDLRGEN007: Decorator must be an open generic class with matching arity
/// - NDLRGEN008: Decorator must implement the interface
/// </summary>
public sealed class OpenDecoratorForAttributeAnalyzerTests
{
    private static string Attributes => NeedlrTestAttributes.AllWithOpenDecorator;

    #region NDLRGEN006: Type must be open generic interface

    [Fact]
    public async Task Error_WhenTypeIsNotGeneric()
    {
        var code = @"
using NexusLabs.Needlr.Generators;

public interface IService { }

[{|#0:OpenDecoratorFor(typeof(IService))|}]
public class ServiceDecorator<T> : IService
{
}
" + Attributes;

        var test = new CSharpAnalyzerTest<OpenDecoratorForAttributeAnalyzer, DefaultVerifier>
        {
            TestCode = code,
            ExpectedDiagnostics =
            {
                new DiagnosticResult("NDLRGEN006", Microsoft.CodeAnalysis.DiagnosticSeverity.Error)
                    .WithLocation(0)
                    .WithArguments("IService", "non-generic interface")
            }
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Error_WhenTypeIsClass()
    {
        var code = @"
using NexusLabs.Needlr.Generators;

public class SomeClass<T> { }

[{|#0:OpenDecoratorFor(typeof(SomeClass<>))|}]
public class ServiceDecorator<T> : SomeClass<T>
{
}
" + Attributes;

        var test = new CSharpAnalyzerTest<OpenDecoratorForAttributeAnalyzer, DefaultVerifier>
        {
            TestCode = code,
            ExpectedDiagnostics =
            {
                new DiagnosticResult("NDLRGEN006", Microsoft.CodeAnalysis.DiagnosticSeverity.Error)
                    .WithLocation(0)
                    .WithArguments("SomeClass<>", "Class (not an interface)")
            }
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task NoError_WhenTypeIsOpenGenericInterface()
    {
        var code = @"
using NexusLabs.Needlr.Generators;

public interface IHandler<T> { }

[OpenDecoratorFor(typeof(IHandler<>))]
public class LoggingDecorator<T> : IHandler<T>
{
}
" + Attributes;

        var test = new CSharpAnalyzerTest<OpenDecoratorForAttributeAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    #endregion

    #region NDLRGEN007: Decorator must be open generic with matching arity

    [Fact]
    public async Task Error_WhenDecoratorIsNotGeneric()
    {
        var code = @"
using NexusLabs.Needlr.Generators;

public interface IHandler<T> { }

[{|#0:OpenDecoratorFor(typeof(IHandler<>))|}]
public class LoggingDecorator : IHandler<string>
{
}
" + Attributes;

        var test = new CSharpAnalyzerTest<OpenDecoratorForAttributeAnalyzer, DefaultVerifier>
        {
            TestCode = code,
            ExpectedDiagnostics =
            {
                new DiagnosticResult("NDLRGEN007", Microsoft.CodeAnalysis.DiagnosticSeverity.Error)
                    .WithLocation(0)
                    .WithArguments("LoggingDecorator", "IHandler<>", 1)
            }
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Error_WhenArityDoesNotMatch()
    {
        var code = @"
using NexusLabs.Needlr.Generators;

public interface IHandler<TMessage, TResult> { }

[{|#0:OpenDecoratorFor(typeof(IHandler<,>))|}]
public class LoggingDecorator<T> : IHandler<T, string>
{
}
" + Attributes;

        var test = new CSharpAnalyzerTest<OpenDecoratorForAttributeAnalyzer, DefaultVerifier>
        {
            TestCode = code,
            ExpectedDiagnostics =
            {
                new DiagnosticResult("NDLRGEN007", Microsoft.CodeAnalysis.DiagnosticSeverity.Error)
                    .WithLocation(0)
                    .WithArguments("LoggingDecorator", "IHandler<,>", 2)
            }
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task NoError_WhenArityMatches()
    {
        var code = @"
using NexusLabs.Needlr.Generators;

public interface IHandler<TMessage, TResult> { }

[OpenDecoratorFor(typeof(IHandler<,>))]
public class LoggingDecorator<TMessage, TResult> : IHandler<TMessage, TResult>
{
}
" + Attributes;

        var test = new CSharpAnalyzerTest<OpenDecoratorForAttributeAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    #endregion

    #region NDLRGEN008: Decorator must implement the interface

    [Fact]
    public async Task Error_WhenDecoratorDoesNotImplementInterface()
    {
        var code = @"
using NexusLabs.Needlr.Generators;

public interface IHandler<T> { }
public interface IOther<T> { }

[{|#0:OpenDecoratorFor(typeof(IHandler<>))|}]
public class LoggingDecorator<T> : IOther<T>
{
}
" + Attributes;

        var test = new CSharpAnalyzerTest<OpenDecoratorForAttributeAnalyzer, DefaultVerifier>
        {
            TestCode = code,
            ExpectedDiagnostics =
            {
                new DiagnosticResult("NDLRGEN008", Microsoft.CodeAnalysis.DiagnosticSeverity.Error)
                    .WithLocation(0)
                    .WithArguments("LoggingDecorator", "IHandler<>")
            }
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task NoError_WhenDecoratorImplementsInterface()
    {
        var code = @"
using NexusLabs.Needlr.Generators;

public interface IHandler<T> { }

[OpenDecoratorFor(typeof(IHandler<>))]
public class LoggingDecorator<T> : IHandler<T>
{
    private readonly IHandler<T> _inner;
    public LoggingDecorator(IHandler<T> inner) => _inner = inner;
}
" + Attributes;

        var test = new CSharpAnalyzerTest<OpenDecoratorForAttributeAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    #endregion

    #region Multiple decorators

    [Fact]
    public async Task NoError_WhenMultipleOpenDecoratorsWithDifferentOrder()
    {
        var code = @"
using NexusLabs.Needlr.Generators;

public interface IHandler<T> { }

[OpenDecoratorFor(typeof(IHandler<>), Order = 1)]
public class LoggingDecorator<T> : IHandler<T>
{
    private readonly IHandler<T> _inner;
    public LoggingDecorator(IHandler<T> inner) => _inner = inner;
}

[OpenDecoratorFor(typeof(IHandler<>), Order = 2)]
public class CachingDecorator<T> : IHandler<T>
{
    private readonly IHandler<T> _inner;
    public CachingDecorator(IHandler<T> inner) => _inner = inner;
}
" + Attributes;

        var test = new CSharpAnalyzerTest<OpenDecoratorForAttributeAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    #endregion
}
