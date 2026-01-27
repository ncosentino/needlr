using System.Threading.Tasks;

using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

using Xunit;

namespace NexusLabs.Needlr.Analyzers.Tests;

public sealed class RegisterAsAttributeAnalyzerTests
{
    private static string Attributes => NeedlrTestAttributes.AllWithRegisterAs;

    #region NDLRCOR015: Type argument not implemented

    [Fact]
    public async Task Error_WhenTypeArgNotImplemented()
    {
        var code = @"
using NexusLabs.Needlr;

public interface IMyService { }
public interface IOtherService { }

[{|#0:RegisterAs<IOtherService>|}]
public class MyService : IMyService
{
}
" + Attributes;

        var test = new CSharpAnalyzerTest<RegisterAsAttributeAnalyzer, DefaultVerifier>
        {
            TestCode = code,
            ExpectedDiagnostics =
            {
                new DiagnosticResult(DiagnosticIds.RegisterAsTypeArgNotImplemented, Microsoft.CodeAnalysis.DiagnosticSeverity.Error)
                    .WithLocation(0)
                    .WithArguments("MyService", "IOtherService")
            }
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Error_WhenTypeArgIsConcreteClass()
    {
        var code = @"
using NexusLabs.Needlr;

public interface IMyService { }
public class SomeClass { }

[{|#0:RegisterAs<SomeClass>|}]
public class MyService : IMyService
{
}
" + Attributes;

        var test = new CSharpAnalyzerTest<RegisterAsAttributeAnalyzer, DefaultVerifier>
        {
            TestCode = code,
            ExpectedDiagnostics =
            {
                new DiagnosticResult(DiagnosticIds.RegisterAsTypeArgNotImplemented, Microsoft.CodeAnalysis.DiagnosticSeverity.Error)
                    .WithLocation(0)
                    .WithArguments("MyService", "SomeClass")
            }
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task NoError_WhenTypeArgIsImplemented()
    {
        var code = @"
using NexusLabs.Needlr;

public interface IMyService { }
public interface IOtherService { }

[RegisterAs<IMyService>]
public class MyService : IMyService, IOtherService
{
}
" + Attributes;

        var test = new CSharpAnalyzerTest<RegisterAsAttributeAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task NoError_WhenTypeArgIsBaseInterface()
    {
        var code = @"
using NexusLabs.Needlr;

public interface IBaseService { }
public interface IMyService : IBaseService { }

[RegisterAs<IBaseService>]
public class MyService : IMyService
{
}
" + Attributes;

        var test = new CSharpAnalyzerTest<RegisterAsAttributeAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task NoError_WithMultipleValidRegisterAs()
    {
        var code = @"
using NexusLabs.Needlr;

public interface IReader { }
public interface IWriter { }
public interface ILogger { }

[RegisterAs<IReader>]
[RegisterAs<IWriter>]
public class MultiService : IReader, IWriter, ILogger
{
}
" + Attributes;

        var test = new CSharpAnalyzerTest<RegisterAsAttributeAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Error_WithMultipleRegisterAs_OneInvalid()
    {
        var code = @"
using NexusLabs.Needlr;

public interface IReader { }
public interface IWriter { }
public interface ILogger { }

[RegisterAs<IReader>]
[{|#0:RegisterAs<ILogger>|}]
public class MultiService : IReader, IWriter
{
}
" + Attributes;

        var test = new CSharpAnalyzerTest<RegisterAsAttributeAnalyzer, DefaultVerifier>
        {
            TestCode = code,
            ExpectedDiagnostics =
            {
                new DiagnosticResult(DiagnosticIds.RegisterAsTypeArgNotImplemented, Microsoft.CodeAnalysis.DiagnosticSeverity.Error)
                    .WithLocation(0)
                    .WithArguments("MultiService", "ILogger")
            }
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    #endregion
}
