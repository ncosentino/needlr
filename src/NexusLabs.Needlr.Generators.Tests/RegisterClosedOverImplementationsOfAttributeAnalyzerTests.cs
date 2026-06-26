using System.Threading.Tasks;

using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

using Xunit;

namespace NexusLabs.Needlr.Generators.Tests;

/// <summary>
/// Tests for RegisterClosedOverImplementationsOfAttributeAnalyzer diagnostics:
/// - NDLRGEN035: Source type must be an open generic interface
/// - NDLRGEN036: Composition must be an open generic class with matching arity
/// - NDLRGEN037: Composition must implement the As service type
/// </summary>
public sealed class RegisterClosedOverImplementationsOfAttributeAnalyzerTests
{
    private static string Attributes => NeedlrTestAttributes.AllWithComposed;

    [Fact]
    public async Task Error_WhenSourceTypeIsNotGeneric()
    {
        var code = @"
using NexusLabs.Needlr.Generators;

public interface IService { }
public interface IFacade { }

[{|#0:RegisterClosedOverImplementationsOf(typeof(IService), As = typeof(IFacade))|}]
public class Composition<T> : IFacade
{
}
" + Attributes;

        var test = new CSharpAnalyzerTest<RegisterClosedOverImplementationsOfAttributeAnalyzer, DefaultVerifier>
        {
            TestCode = code,
            ExpectedDiagnostics =
            {
                new DiagnosticResult("NDLRGEN035", Microsoft.CodeAnalysis.DiagnosticSeverity.Error)
                    .WithLocation(0)
                    .WithArguments("IService", "non-generic interface")
            }
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Error_WhenSourceTypeIsClass()
    {
        var code = @"
using NexusLabs.Needlr.Generators;

public class SomeClass<T> { }
public interface IFacade { }

[{|#0:RegisterClosedOverImplementationsOf(typeof(SomeClass<>), As = typeof(IFacade))|}]
public class Composition<T> : IFacade
{
}
" + Attributes;

        var test = new CSharpAnalyzerTest<RegisterClosedOverImplementationsOfAttributeAnalyzer, DefaultVerifier>
        {
            TestCode = code,
            ExpectedDiagnostics =
            {
                new DiagnosticResult("NDLRGEN035", Microsoft.CodeAnalysis.DiagnosticSeverity.Error)
                    .WithLocation(0)
                    .WithArguments("SomeClass<>", "Class (not an interface)")
            }
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task NoError_WhenSourceIsOpenGenericInterface()
    {
        var code = @"
using NexusLabs.Needlr.Generators;

public interface IDefinition<T> { }
public interface IFacade { }

[RegisterClosedOverImplementationsOf(typeof(IDefinition<>), As = typeof(IFacade))]
public class Composition<T> : IFacade
{
}
" + Attributes;

        var test = new CSharpAnalyzerTest<RegisterClosedOverImplementationsOfAttributeAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Error_WhenCompositionIsNotGeneric()
    {
        var code = @"
using NexusLabs.Needlr.Generators;

public interface IDefinition<T> { }
public interface IFacade { }

[{|#0:RegisterClosedOverImplementationsOf(typeof(IDefinition<>), As = typeof(IFacade))|}]
public class Composition : IFacade
{
}
" + Attributes;

        var test = new CSharpAnalyzerTest<RegisterClosedOverImplementationsOfAttributeAnalyzer, DefaultVerifier>
        {
            TestCode = code,
            ExpectedDiagnostics =
            {
                new DiagnosticResult("NDLRGEN036", Microsoft.CodeAnalysis.DiagnosticSeverity.Error)
                    .WithLocation(0)
                    .WithArguments("Composition", "IDefinition<>", 1)
            }
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Error_WhenArityDoesNotMatch()
    {
        var code = @"
using NexusLabs.Needlr.Generators;

public interface IDefinition<TKey, TValue> { }
public interface IFacade { }

[{|#0:RegisterClosedOverImplementationsOf(typeof(IDefinition<,>), As = typeof(IFacade))|}]
public class Composition<T> : IFacade
{
}
" + Attributes;

        var test = new CSharpAnalyzerTest<RegisterClosedOverImplementationsOfAttributeAnalyzer, DefaultVerifier>
        {
            TestCode = code,
            ExpectedDiagnostics =
            {
                new DiagnosticResult("NDLRGEN036", Microsoft.CodeAnalysis.DiagnosticSeverity.Error)
                    .WithLocation(0)
                    .WithArguments("Composition", "IDefinition<,>", 2)
            }
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task NoError_WhenArityMatches()
    {
        var code = @"
using NexusLabs.Needlr.Generators;

public interface IDefinition<TKey, TValue> { }
public interface IFacade { }

[RegisterClosedOverImplementationsOf(typeof(IDefinition<,>), As = typeof(IFacade))]
public class Composition<TKey, TValue> : IFacade
{
}
" + Attributes;

        var test = new CSharpAnalyzerTest<RegisterClosedOverImplementationsOfAttributeAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Error_WhenCompositionDoesNotImplementAs()
    {
        var code = @"
using NexusLabs.Needlr.Generators;

public interface IDefinition<T> { }
public interface IFacade { }
public interface IOther { }

[{|#0:RegisterClosedOverImplementationsOf(typeof(IDefinition<>), As = typeof(IFacade))|}]
public class Composition<T> : IOther
{
}
" + Attributes;

        var test = new CSharpAnalyzerTest<RegisterClosedOverImplementationsOfAttributeAnalyzer, DefaultVerifier>
        {
            TestCode = code,
            ExpectedDiagnostics =
            {
                new DiagnosticResult("NDLRGEN037", Microsoft.CodeAnalysis.DiagnosticSeverity.Error)
                    .WithLocation(0)
                    .WithArguments("Composition")
            }
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Error_WhenAsNotSpecified()
    {
        var code = @"
using NexusLabs.Needlr.Generators;

public interface IDefinition<T> { }
public interface IFacade { }

[{|#0:RegisterClosedOverImplementationsOf(typeof(IDefinition<>))|}]
public class Composition<T> : IFacade
{
}
" + Attributes;

        var test = new CSharpAnalyzerTest<RegisterClosedOverImplementationsOfAttributeAnalyzer, DefaultVerifier>
        {
            TestCode = code,
            ExpectedDiagnostics =
            {
                new DiagnosticResult("NDLRGEN037", Microsoft.CodeAnalysis.DiagnosticSeverity.Error)
                    .WithLocation(0)
                    .WithArguments("Composition")
            }
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task NoError_WhenValid()
    {
        var code = @"
using NexusLabs.Needlr.Generators;

public interface IDefinition<T> { }
public interface IFacade { }

[RegisterClosedOverImplementationsOf(typeof(IDefinition<>), As = typeof(IFacade))]
public class Composition<T> : IFacade
{
}
" + Attributes;

        var test = new CSharpAnalyzerTest<RegisterClosedOverImplementationsOfAttributeAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }
}
