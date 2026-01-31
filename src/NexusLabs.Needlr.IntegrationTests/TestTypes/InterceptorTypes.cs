namespace NexusLabs.Needlr.IntegrationTests;

/// <summary>
/// Interface for testing basic interceptor functionality with logging interceptor.
/// </summary>
public interface ILoggingInterceptedService
{
    string GetValue();
    string Process(string input);
    Task<string> GetValueAsync();
    void DoWork();
}

/// <summary>
/// Interface for testing modifying interceptor functionality.
/// </summary>
public interface IModifyingInterceptedService
{
    string GetValue();
    string Process(string input);
    Task<string> GetValueAsync();
    void DoWork();
}

/// <summary>
/// Interface for testing multi-interceptor chains.
/// </summary>
public interface IMultiInterceptedService
{
    string GetValue();
    string Process(string input);
    Task<string> GetValueAsync();
    void DoWork();
}

/// <summary>
/// A logging interceptor that captures method calls for testing.
/// </summary>
public sealed class TestLoggingInterceptor : IMethodInterceptor
{
    private readonly List<string> _log = new();

    public IReadOnlyList<string> Log => _log;

    public async ValueTask<object?> InterceptAsync(IMethodInvocation invocation)
    {
        _log.Add($"Before:{invocation.Method.Name}");
        var result = await invocation.ProceedAsync();
        _log.Add($"After:{invocation.Method.Name}");
        return result;
    }
}

/// <summary>
/// An interceptor that modifies return values.
/// </summary>
public sealed class TestModifyingInterceptor : IMethodInterceptor
{
    public async ValueTask<object?> InterceptAsync(IMethodInvocation invocation)
    {
        var result = await invocation.ProceedAsync();
        if (result is string str)
        {
            return $"[Modified:{str}]";
        }
        return result;
    }
}

/// <summary>
/// An interceptor that wraps results with order info.
/// </summary>
public sealed class TestOrderedInterceptor1 : IMethodInterceptor
{
    public async ValueTask<object?> InterceptAsync(IMethodInvocation invocation)
    {
        var result = await invocation.ProceedAsync();
        if (result is string str)
        {
            return $"Order1({str})";
        }
        return result;
    }
}

/// <summary>
/// Second ordered interceptor for testing chain order.
/// </summary>
public sealed class TestOrderedInterceptor2 : IMethodInterceptor
{
    public async ValueTask<object?> InterceptAsync(IMethodInvocation invocation)
    {
        var result = await invocation.ProceedAsync();
        if (result is string str)
        {
            return $"Order2({str})";
        }
        return result;
    }
}

/// <summary>
/// Service with class-level logging interceptor (non-modifying).
/// </summary>
[Intercept<TestLoggingInterceptor>]
public sealed class InterceptedTestService : ILoggingInterceptedService
{
    public string GetValue() => "Original";
    public string Process(string input) => $"Processed:{input}";
    public Task<string> GetValueAsync() => Task.FromResult("AsyncOriginal");
    public void DoWork() { }
}

/// <summary>
/// Service with multiple ordered interceptors.
/// Order 1 executes first (outermost), Order 2 executes second (inner).
/// Result: Order1(Order2(Original))
/// </summary>
[Intercept<TestOrderedInterceptor1>(Order = 1)]
[Intercept<TestOrderedInterceptor2>(Order = 2)]
public sealed class MultiInterceptedTestService : IMultiInterceptedService
{
    public string GetValue() => "Original";
    public string Process(string input) => $"Processed:{input}";
    public Task<string> GetValueAsync() => Task.FromResult("AsyncOriginal");
    public void DoWork() { }
}

/// <summary>
/// Service with modifying interceptor for testing return value modification.
/// </summary>
[Intercept<TestModifyingInterceptor>]
public sealed class ModifyingInterceptedTestService : IModifyingInterceptedService
{
    public string GetValue() => "Original";
    public string Process(string input) => $"Processed:{input}";
    public Task<string> GetValueAsync() => Task.FromResult("AsyncOriginal");
    public void DoWork() { }
}
