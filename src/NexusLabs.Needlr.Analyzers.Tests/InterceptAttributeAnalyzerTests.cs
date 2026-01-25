using System.Threading.Tasks;

using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

using Xunit;

namespace NexusLabs.Needlr.Analyzers.Tests;

public sealed class InterceptAttributeAnalyzerTests
{
    // Use shared test attributes that match the real package
    private static string Attributes => NeedlrTestAttributes.Interceptors;

    [Fact]
    public async Task NoError_WhenInterceptorImplementsInterface()
    {
        var code = @"
using NexusLabs.Needlr;

public interface IOrderService
{
    void PlaceOrder();
}

[Intercept<LoggingInterceptor>]
public class OrderService : IOrderService
{
    public void PlaceOrder() { }
}

public class LoggingInterceptor : IMethodInterceptor
{
    public System.Threading.Tasks.ValueTask<object?> InterceptAsync(IMethodInvocation invocation)
    {
        return invocation.ProceedAsync();
    }
}
" + Attributes;

        var test = new CSharpAnalyzerTest<InterceptAttributeAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Error_WhenInterceptorDoesNotImplementInterface()
    {
        var code = @"
using NexusLabs.Needlr;

public interface IOrderService
{
    void PlaceOrder();
}

[{|#0:Intercept(typeof(NotAnInterceptor))|}]
public class OrderService : IOrderService
{
    public void PlaceOrder() { }
}

public class NotAnInterceptor
{
    public void DoSomething() { }
}
" + Attributes;

        var test = new CSharpAnalyzerTest<InterceptAttributeAnalyzer, DefaultVerifier>
        {
            TestCode = code,
            ExpectedDiagnostics =
            {
                new DiagnosticResult(DiagnosticIds.InterceptTypeMustImplementInterface, Microsoft.CodeAnalysis.DiagnosticSeverity.Error)
                    .WithLocation(0)
                    .WithArguments("NotAnInterceptor")
            }
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Warning_WhenClassHasNoInterfaces()
    {
        var code = @"
using NexusLabs.Needlr;

[{|#0:Intercept<LoggingInterceptor>|}]
public class OrderService
{
    public void PlaceOrder() { }
}

public class LoggingInterceptor : IMethodInterceptor
{
    public System.Threading.Tasks.ValueTask<object?> InterceptAsync(IMethodInvocation invocation)
    {
        return invocation.ProceedAsync();
    }
}
" + Attributes;

        var test = new CSharpAnalyzerTest<InterceptAttributeAnalyzer, DefaultVerifier>
        {
            TestCode = code,
            ExpectedDiagnostics =
            {
                new DiagnosticResult(DiagnosticIds.InterceptOnClassWithoutInterfaces, Microsoft.CodeAnalysis.DiagnosticSeverity.Warning)
                    .WithLocation(0)
                    .WithArguments("OrderService")
            }
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task NoWarning_WhenMethodLevelInterceptorOnClassWithInterface()
    {
        var code = @"
using NexusLabs.Needlr;

public interface ICalculator
{
    int Add(int a, int b);
    int Multiply(int a, int b);
}

public class Calculator : ICalculator
{
    public int Add(int a, int b) => a + b;

    [Intercept<TimingInterceptor>]
    public int Multiply(int a, int b) => a * b;
}

public class TimingInterceptor : IMethodInterceptor
{
    public System.Threading.Tasks.ValueTask<object?> InterceptAsync(IMethodInvocation invocation)
    {
        return invocation.ProceedAsync();
    }
}
" + Attributes;

        var test = new CSharpAnalyzerTest<InterceptAttributeAnalyzer, DefaultVerifier>
        {
            TestCode = code
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task NoWarning_WhenClassImplementsSystemInterface()
    {
        // IDisposable is a system interface, shouldn't count as "user interface"
        var code = @"
using NexusLabs.Needlr;
using System;

[{|#0:Intercept<LoggingInterceptor>|}]
public class OrderService : IDisposable
{
    public void Dispose() { }
}

public class LoggingInterceptor : IMethodInterceptor
{
    public System.Threading.Tasks.ValueTask<object?> InterceptAsync(IMethodInvocation invocation)
    {
        return invocation.ProceedAsync();
    }
}
" + Attributes;

        var test = new CSharpAnalyzerTest<InterceptAttributeAnalyzer, DefaultVerifier>
        {
            TestCode = code,
            ExpectedDiagnostics =
            {
                // Should still warn because IDisposable is a system interface
                new DiagnosticResult(DiagnosticIds.InterceptOnClassWithoutInterfaces, Microsoft.CodeAnalysis.DiagnosticSeverity.Warning)
                    .WithLocation(0)
                    .WithArguments("OrderService")
            }
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }
}
