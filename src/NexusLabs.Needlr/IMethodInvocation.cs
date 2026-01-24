using System.Reflection;

namespace NexusLabs.Needlr;

/// <summary>
/// Represents a method invocation that is being intercepted.
/// Provides access to the target instance, method metadata, and arguments.
/// </summary>
public interface IMethodInvocation
{
    /// <summary>
    /// Gets the target service instance on which the method is being invoked.
    /// </summary>
    object Target { get; }

    /// <summary>
    /// Gets metadata about the method being invoked.
    /// </summary>
    MethodInfo Method { get; }

    /// <summary>
    /// Gets the arguments passed to the method. The array can be modified
    /// to change argument values before calling <see cref="ProceedAsync"/>.
    /// </summary>
    object?[] Arguments { get; }

    /// <summary>
    /// Gets the generic type arguments if the method is a generic method.
    /// Returns an empty array for non-generic methods.
    /// </summary>
    Type[] GenericArguments { get; }

    /// <summary>
    /// Proceeds to the next interceptor in the chain, or to the actual
    /// method implementation if this is the last interceptor.
    /// </summary>
    /// <returns>
    /// The result of the method (or the result from the next interceptor).
    /// Returns <c>null</c> for void methods.
    /// </returns>
    ValueTask<object?> ProceedAsync();
}
