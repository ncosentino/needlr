namespace NexusLabs.Needlr;

/// <summary>
/// Intercepts method invocations on a service. Implement this interface
/// to create reusable cross-cutting concerns like logging, caching, or timing.
/// </summary>
/// <remarks>
/// <para>
/// Unlike decorators (which require implementing every method of an interface),
/// interceptors handle any method invocation with a single implementation.
/// Apply interceptors to services using <see cref="InterceptAttribute{TInterceptor}"/>.
/// </para>
/// <para>
/// Interceptors are resolved from the DI container and can have their own dependencies.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// public class LoggingInterceptor : IMethodInterceptor
/// {
///     private readonly ILogger _logger;
///     
///     public LoggingInterceptor(ILogger logger) => _logger = logger;
///     
///     public async ValueTask&lt;object?&gt; InterceptAsync(IMethodInvocation invocation)
///     {
///         _logger.LogInformation("Calling {Method}", invocation.Method.Name);
///         var sw = Stopwatch.StartNew();
///         try
///         {
///             var result = await invocation.ProceedAsync();
///             _logger.LogInformation("{Method} completed in {Elapsed}ms", 
///                 invocation.Method.Name, sw.ElapsedMilliseconds);
///             return result;
///         }
///         catch (Exception ex)
///         {
///             _logger.LogError(ex, "{Method} failed", invocation.Method.Name);
///             throw;
///         }
///     }
/// }
/// </code>
/// </example>
public interface IMethodInterceptor
{
    /// <summary>
    /// Called when an intercepted method is invoked. Call 
    /// <see cref="IMethodInvocation.ProceedAsync"/> to continue to the next
    /// interceptor in the chain or to the actual method implementation.
    /// </summary>
    /// <param name="invocation">
    /// Context about the method being invoked, including the target instance,
    /// method metadata, and arguments.
    /// </param>
    /// <returns>
    /// The result of the method invocation (or the modified result if the
    /// interceptor transforms it). For void methods, return <c>null</c>.
    /// </returns>
    ValueTask<object?> InterceptAsync(IMethodInvocation invocation);
}
