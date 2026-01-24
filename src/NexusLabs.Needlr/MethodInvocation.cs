using System.Reflection;

namespace NexusLabs.Needlr;

/// <summary>
/// Default implementation of <see cref="IMethodInvocation"/> used by generated
/// interceptor proxy classes.
/// </summary>
public sealed class MethodInvocation : IMethodInvocation
{
    private readonly Func<ValueTask<object?>> _proceed;
    private bool _proceeded;

    /// <summary>
    /// Initializes a new instance of the <see cref="MethodInvocation"/> class.
    /// </summary>
    /// <param name="target">The target service instance.</param>
    /// <param name="method">The method being invoked.</param>
    /// <param name="arguments">The arguments passed to the method.</param>
    /// <param name="genericArguments">The generic type arguments (empty for non-generic methods).</param>
    /// <param name="proceed">
    /// A function that invokes the next interceptor or the actual method.
    /// </param>
    public MethodInvocation(
        object target,
        MethodInfo method,
        object?[] arguments,
        Type[] genericArguments,
        Func<ValueTask<object?>> proceed)
    {
        Target = target ?? throw new ArgumentNullException(nameof(target));
        Method = method ?? throw new ArgumentNullException(nameof(method));
        Arguments = arguments ?? throw new ArgumentNullException(nameof(arguments));
        GenericArguments = genericArguments ?? throw new ArgumentNullException(nameof(genericArguments));
        _proceed = proceed ?? throw new ArgumentNullException(nameof(proceed));
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="MethodInvocation"/> class
    /// for non-generic methods.
    /// </summary>
    /// <param name="target">The target service instance.</param>
    /// <param name="method">The method being invoked.</param>
    /// <param name="arguments">The arguments passed to the method.</param>
    /// <param name="proceed">
    /// A function that invokes the next interceptor or the actual method.
    /// </param>
    public MethodInvocation(
        object target,
        MethodInfo method,
        object?[] arguments,
        Func<ValueTask<object?>> proceed)
        : this(target, method, arguments, Type.EmptyTypes, proceed)
    {
    }

    /// <inheritdoc />
    public object Target { get; }

    /// <inheritdoc />
    public MethodInfo Method { get; }

    /// <inheritdoc />
    public object?[] Arguments { get; }

    /// <inheritdoc />
    public Type[] GenericArguments { get; }

    /// <inheritdoc />
    public ValueTask<object?> ProceedAsync()
    {
        if (_proceeded)
        {
            throw new InvalidOperationException(
                "ProceedAsync has already been called on this invocation. " +
                "Each interceptor should call ProceedAsync at most once.");
        }
        _proceeded = true;
        return _proceed();
    }
}
