using System.Reflection;

using Xunit;

namespace NexusLabs.Needlr.Tests;

/// <summary>
/// Tests for the interceptor infrastructure: IMethodInterceptor, IMethodInvocation,
/// MethodInvocation, and InterceptAttribute.
/// </summary>
public sealed class InterceptorTests
{
    #region MethodInvocation Tests

    [Fact]
    public async Task MethodInvocation_ProceedAsync_CallsProvidedFunction()
    {
        var wasCalled = false;
        var target = new TestService();
        var method = typeof(ITestService).GetMethod(nameof(ITestService.GetValue))!;

        var invocation = new MethodInvocation(
            target,
            method,
            Array.Empty<object?>(),
            () =>
            {
                wasCalled = true;
                return ValueTask.FromResult<object?>("result");
            });

        var result = await invocation.ProceedAsync();

        Assert.True(wasCalled);
        Assert.Equal("result", result);
    }

    [Fact]
    public async Task MethodInvocation_ProceedAsync_CanOnlyBeCalledOnce()
    {
        var target = new TestService();
        var method = typeof(ITestService).GetMethod(nameof(ITestService.GetValue))!;

        var invocation = new MethodInvocation(
            target,
            method,
            Array.Empty<object?>(),
            () => ValueTask.FromResult<object?>("result"));

        await invocation.ProceedAsync();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await invocation.ProceedAsync());

        Assert.Contains("already been called", ex.Message);
    }

    [Fact]
    public void MethodInvocation_ExposesTargetCorrectly()
    {
        var target = new TestService();
        var method = typeof(ITestService).GetMethod(nameof(ITestService.GetValue))!;

        var invocation = new MethodInvocation(
            target,
            method,
            Array.Empty<object?>(),
            () => ValueTask.FromResult<object?>(null));

        Assert.Same(target, invocation.Target);
    }

    [Fact]
    public void MethodInvocation_ExposesMethodInfoCorrectly()
    {
        var target = new TestService();
        var method = typeof(ITestService).GetMethod(nameof(ITestService.GetValue))!;

        var invocation = new MethodInvocation(
            target,
            method,
            Array.Empty<object?>(),
            () => ValueTask.FromResult<object?>(null));

        Assert.Same(method, invocation.Method);
        Assert.Equal(nameof(ITestService.GetValue), invocation.Method.Name);
    }

    [Fact]
    public void MethodInvocation_ExposesArgumentsCorrectly()
    {
        var target = new TestService();
        var method = typeof(ITestService).GetMethod(nameof(ITestService.ProcessData))!;
        var args = new object?[] { 42, "test" };

        var invocation = new MethodInvocation(
            target,
            method,
            args,
            () => ValueTask.FromResult<object?>(null));

        Assert.Equal(2, invocation.Arguments.Length);
        Assert.Equal(42, invocation.Arguments[0]);
        Assert.Equal("test", invocation.Arguments[1]);
    }

    [Fact]
    public void MethodInvocation_Arguments_CanBeModified()
    {
        var target = new TestService();
        var method = typeof(ITestService).GetMethod(nameof(ITestService.ProcessData))!;
        var args = new object?[] { 42, "test" };

        var invocation = new MethodInvocation(
            target,
            method,
            args,
            () => ValueTask.FromResult<object?>(null));

        invocation.Arguments[0] = 100;
        invocation.Arguments[1] = "modified";

        Assert.Equal(100, invocation.Arguments[0]);
        Assert.Equal("modified", invocation.Arguments[1]);
    }

    [Fact]
    public void MethodInvocation_GenericArguments_AreEmptyForNonGenericMethods()
    {
        var target = new TestService();
        var method = typeof(ITestService).GetMethod(nameof(ITestService.GetValue))!;

        var invocation = new MethodInvocation(
            target,
            method,
            Array.Empty<object?>(),
            () => ValueTask.FromResult<object?>(null));

        Assert.Empty(invocation.GenericArguments);
    }

    [Fact]
    public void MethodInvocation_GenericArguments_AreProvidedForGenericMethods()
    {
        var target = new TestService();
        var method = typeof(ITestService).GetMethod(nameof(ITestService.GetValue))!;
        var genericArgs = new[] { typeof(string), typeof(int) };

        var invocation = new MethodInvocation(
            target,
            method,
            Array.Empty<object?>(),
            genericArgs,
            () => ValueTask.FromResult<object?>(null));

        Assert.Equal(2, invocation.GenericArguments.Length);
        Assert.Equal(typeof(string), invocation.GenericArguments[0]);
        Assert.Equal(typeof(int), invocation.GenericArguments[1]);
    }

    [Fact]
    public void MethodInvocation_Constructor_ThrowsOnNullTarget()
    {
        var method = typeof(ITestService).GetMethod(nameof(ITestService.GetValue))!;

        Assert.Throws<ArgumentNullException>(() => new MethodInvocation(
            null!,
            method,
            Array.Empty<object?>(),
            () => ValueTask.FromResult<object?>(null)));
    }

    [Fact]
    public void MethodInvocation_Constructor_ThrowsOnNullMethod()
    {
        var target = new TestService();

        Assert.Throws<ArgumentNullException>(() => new MethodInvocation(
            target,
            null!,
            Array.Empty<object?>(),
            () => ValueTask.FromResult<object?>(null)));
    }

    [Fact]
    public void MethodInvocation_Constructor_ThrowsOnNullArguments()
    {
        var target = new TestService();
        var method = typeof(ITestService).GetMethod(nameof(ITestService.GetValue))!;

        Assert.Throws<ArgumentNullException>(() => new MethodInvocation(
            target,
            method,
            null!,
            () => ValueTask.FromResult<object?>(null)));
    }

    [Fact]
    public void MethodInvocation_Constructor_ThrowsOnNullProceed()
    {
        var target = new TestService();
        var method = typeof(ITestService).GetMethod(nameof(ITestService.GetValue))!;

        Assert.Throws<ArgumentNullException>(() => new MethodInvocation(
            target,
            method,
            Array.Empty<object?>(),
            null!));
    }

    #endregion

    #region InterceptAttribute Tests

    [Fact]
    public void InterceptAttribute_StoresInterceptorType()
    {
        var attr = new InterceptAttribute(typeof(LoggingInterceptor));

        Assert.Equal(typeof(LoggingInterceptor), attr.InterceptorType);
    }

    [Fact]
    public void InterceptAttribute_DefaultOrderIsZero()
    {
        var attr = new InterceptAttribute(typeof(LoggingInterceptor));

        Assert.Equal(0, attr.Order);
    }

    [Fact]
    public void InterceptAttribute_OrderCanBeSet()
    {
        var attr = new InterceptAttribute(typeof(LoggingInterceptor)) { Order = 5 };

        Assert.Equal(5, attr.Order);
    }

    [Fact]
    public void InterceptAttribute_ThrowsOnNullType()
    {
        Assert.Throws<ArgumentNullException>(() => new InterceptAttribute(null!));
    }

    [Fact]
    public void InterceptAttribute_Generic_StoresInterceptorType()
    {
        var attr = new InterceptAttribute<LoggingInterceptor>();

        Assert.Equal(typeof(LoggingInterceptor), attr.InterceptorType);
    }

    [Fact]
    public void InterceptAttribute_Generic_DefaultOrderIsZero()
    {
        var attr = new InterceptAttribute<LoggingInterceptor>();

        Assert.Equal(0, attr.Order);
    }

    [Fact]
    public void InterceptAttribute_Generic_OrderCanBeSet()
    {
        var attr = new InterceptAttribute<LoggingInterceptor> { Order = 10 };

        Assert.Equal(10, attr.Order);
    }

    [Fact]
    public void InterceptAttribute_CanBeAppliedToClass()
    {
        var attrs = typeof(InterceptedService).GetCustomAttributes(
            typeof(InterceptAttribute<LoggingInterceptor>), false);

        Assert.Single(attrs);
    }

    [Fact]
    public void InterceptAttribute_CanBeAppliedToMethod()
    {
        var method = typeof(PartiallyInterceptedService)
            .GetMethod(nameof(PartiallyInterceptedService.InterceptedMethod));
        var attrs = method!.GetCustomAttributes(typeof(InterceptAttribute), false);

        Assert.Single(attrs);
    }

    [Fact]
    public void InterceptAttribute_MultipleCanBeApplied()
    {
        var attrs = typeof(MultiInterceptedService).GetCustomAttributes(false)
            .Where(a => a.GetType().Name.StartsWith("InterceptAttribute"))
            .ToList();

        Assert.Equal(2, attrs.Count);
    }

    #endregion

    #region IMethodInterceptor Implementation Tests

    [Fact]
    public async Task Interceptor_CanModifyReturnValue()
    {
        var interceptor = new ValueModifyingInterceptor();
        var target = new TestService();
        var method = typeof(ITestService).GetMethod(nameof(ITestService.GetValue))!;

        var invocation = new MethodInvocation(
            target,
            method,
            Array.Empty<object?>(),
            () => ValueTask.FromResult<object?>("original"));

        var result = await interceptor.InterceptAsync(invocation);

        Assert.Equal("modified: original", result);
    }

    [Fact]
    public async Task Interceptor_CanSkipProceed()
    {
        var interceptor = new ShortCircuitInterceptor();
        var target = new TestService();
        var method = typeof(ITestService).GetMethod(nameof(ITestService.GetValue))!;
        var proceedCalled = false;

        var invocation = new MethodInvocation(
            target,
            method,
            Array.Empty<object?>(),
            () =>
            {
                proceedCalled = true;
                return ValueTask.FromResult<object?>("original");
            });

        var result = await interceptor.InterceptAsync(invocation);

        Assert.False(proceedCalled);
        Assert.Equal("short-circuited", result);
    }

    [Fact]
    public async Task Interceptor_CanAccessMethodMetadata()
    {
        var interceptor = new MetadataCapturingInterceptor();
        var target = new TestService();
        var method = typeof(ITestService).GetMethod(nameof(ITestService.ProcessData))!;
        var args = new object?[] { 123, "hello" };

        var invocation = new MethodInvocation(
            target,
            method,
            args,
            () => ValueTask.FromResult<object?>("done"));

        await interceptor.InterceptAsync(invocation);

        Assert.Equal(nameof(ITestService.ProcessData), interceptor.CapturedMethodName);
        Assert.Equal(2, interceptor.CapturedArgCount);
        Assert.Equal(123, interceptor.CapturedFirstArg);
    }

    [Fact]
    public async Task Interceptor_Chain_ExecutesInOrder()
    {
        var callOrder = new List<string>();
        var interceptor1 = new OrderTrackingInterceptor("first", callOrder);
        var interceptor2 = new OrderTrackingInterceptor("second", callOrder);

        var target = new TestService();
        var method = typeof(ITestService).GetMethod(nameof(ITestService.GetValue))!;

        // Build the chain: interceptor1 -> interceptor2 -> actual
        var actualInvocation = new MethodInvocation(
            target,
            method,
            Array.Empty<object?>(),
            () =>
            {
                callOrder.Add("actual");
                return ValueTask.FromResult<object?>("result");
            });

        var secondInvocation = new MethodInvocation(
            target,
            method,
            Array.Empty<object?>(),
            async () => await interceptor2.InterceptAsync(actualInvocation));

        await interceptor1.InterceptAsync(secondInvocation);

        Assert.Equal(new[] { "first-before", "second-before", "actual", "second-after", "first-after" }, callOrder);
    }

    #endregion

    #region Test Types

    public interface ITestService
    {
        string GetValue();
        void ProcessData(int id, string name);
    }

    private sealed class TestService : ITestService
    {
        public string GetValue() => "value";
        public void ProcessData(int id, string name) { }
    }

    private sealed class LoggingInterceptor : IMethodInterceptor
    {
        public async ValueTask<object?> InterceptAsync(IMethodInvocation invocation)
        {
            return await invocation.ProceedAsync();
        }
    }

    private sealed class CachingInterceptor : IMethodInterceptor
    {
        public async ValueTask<object?> InterceptAsync(IMethodInvocation invocation)
        {
            return await invocation.ProceedAsync();
        }
    }

    private sealed class ValueModifyingInterceptor : IMethodInterceptor
    {
        public async ValueTask<object?> InterceptAsync(IMethodInvocation invocation)
        {
            var result = await invocation.ProceedAsync();
            return $"modified: {result}";
        }
    }

    private sealed class ShortCircuitInterceptor : IMethodInterceptor
    {
        public ValueTask<object?> InterceptAsync(IMethodInvocation invocation)
        {
            return ValueTask.FromResult<object?>("short-circuited");
        }
    }

    private sealed class MetadataCapturingInterceptor : IMethodInterceptor
    {
        public string? CapturedMethodName { get; private set; }
        public int CapturedArgCount { get; private set; }
        public object? CapturedFirstArg { get; private set; }

        public async ValueTask<object?> InterceptAsync(IMethodInvocation invocation)
        {
            CapturedMethodName = invocation.Method.Name;
            CapturedArgCount = invocation.Arguments.Length;
            CapturedFirstArg = invocation.Arguments.Length > 0 ? invocation.Arguments[0] : null;
            return await invocation.ProceedAsync();
        }
    }

    private sealed class OrderTrackingInterceptor : IMethodInterceptor
    {
        private readonly string _name;
        private readonly List<string> _callOrder;

        public OrderTrackingInterceptor(string name, List<string> callOrder)
        {
            _name = name;
            _callOrder = callOrder;
        }

        public async ValueTask<object?> InterceptAsync(IMethodInvocation invocation)
        {
            _callOrder.Add($"{_name}-before");
            var result = await invocation.ProceedAsync();
            _callOrder.Add($"{_name}-after");
            return result;
        }
    }

    [Intercept<LoggingInterceptor>]
    private sealed class InterceptedService : ITestService
    {
        public string GetValue() => "value";
        public void ProcessData(int id, string name) { }
    }

    private sealed class PartiallyInterceptedService : ITestService
    {
        public string GetValue() => "value";

        [Intercept(typeof(CachingInterceptor))]
        public void InterceptedMethod() { }

        public void ProcessData(int id, string name) { }
    }

    [Intercept<LoggingInterceptor>(Order = 1)]
    [Intercept<CachingInterceptor>(Order = 2)]
    private sealed class MultiInterceptedService : ITestService
    {
        public string GetValue() => "value";
        public void ProcessData(int id, string name) { }
    }

    #endregion
}
