namespace NexusLabs.Needlr;

/// <summary>
/// Applies an interceptor to a class or method. When discovered by Needlr's
/// source generator, a proxy class is generated that routes method calls
/// through the specified interceptor(s).
/// </summary>
/// <remarks>
/// <para>
/// This attribute can be applied at the class level (intercepts all methods)
/// or at the method level (intercepts only that method).
/// </para>
/// <para>
/// The interceptor type must implement <see cref="IMethodInterceptor"/> and
/// will be resolved from the DI container, allowing it to have dependencies.
/// </para>
/// <para>
/// When multiple interceptors are applied, use the <see cref="Order"/> property
/// to control execution order. Lower values execute first (outermost in the chain).
/// </para>
/// <para>
/// <strong>Note:</strong> Interceptors are only supported with source generation.
/// They are not available when using reflection-based registration.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // Class-level interception (all methods)
/// [Intercept(typeof(LoggingInterceptor))]
/// [Scoped]
/// public class OrderService : IOrderService
/// {
///     public Task&lt;Order&gt; GetOrderAsync(int id) => ...;
///     public Task CreateOrderAsync(Order order) => ...;
/// }
/// 
/// // Method-level interception
/// [Scoped]
/// public class ProductService : IProductService
/// {
///     [Intercept(typeof(CachingInterceptor))]
///     public Task&lt;Product&gt; GetProductAsync(int id) => ...;
///     
///     // This method is NOT intercepted
///     public Task UpdateProductAsync(Product product) => ...;
/// }
/// 
/// // Multiple interceptors with ordering
/// [Intercept(typeof(LoggingInterceptor), Order = 1)]
/// [Intercept(typeof(TimingInterceptor), Order = 2)]
/// [Scoped]
/// public class ReportService : IReportService { }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, Inherited = false, AllowMultiple = true)]
public sealed class InterceptAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="InterceptAttribute"/> class.
    /// </summary>
    /// <param name="interceptorType">
    /// The type of the interceptor. Must implement <see cref="IMethodInterceptor"/>.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="interceptorType"/> is null.
    /// </exception>
    public InterceptAttribute(Type interceptorType)
    {
        InterceptorType = interceptorType ?? throw new ArgumentNullException(nameof(interceptorType));
    }

    /// <summary>
    /// Gets the type of the interceptor to apply.
    /// </summary>
    public Type InterceptorType { get; }

    /// <summary>
    /// Gets or sets the order in which this interceptor executes relative to
    /// other interceptors on the same target. Lower values execute first
    /// (outermost in the interceptor chain). Default is 0.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Example with Order values:
    /// <list type="bullet">
    /// <item>Order = 1: LoggingInterceptor (executes first, wraps everything)</item>
    /// <item>Order = 2: CachingInterceptor (executes second)</item>
    /// <item>Order = 3: ValidationInterceptor (executes last, closest to actual method)</item>
    /// </list>
    /// </para>
    /// </remarks>
    public int Order { get; set; } = 0;
}

/// <summary>
/// Applies an interceptor to a class or method. Generic version that provides
/// compile-time type safety for the interceptor type.
/// </summary>
/// <typeparam name="TInterceptor">
/// The type of the interceptor. Must implement <see cref="IMethodInterceptor"/>.
/// </typeparam>
/// <remarks>
/// <para>
/// This is the preferred form when the interceptor type is known at compile time.
/// For dynamic scenarios, use the non-generic <see cref="InterceptAttribute"/>.
/// </para>
/// <para>
/// See <see cref="InterceptAttribute"/> for full documentation and examples.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// [Intercept&lt;LoggingInterceptor&gt;(Order = 1)]
/// [Intercept&lt;CachingInterceptor&gt;(Order = 2)]
/// [Scoped]
/// public class OrderService : IOrderService
/// {
///     public Task&lt;Order&gt; GetOrderAsync(int id) => ...;
/// }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, Inherited = false, AllowMultiple = true)]
public sealed class InterceptAttribute<TInterceptor> : Attribute
    where TInterceptor : class, IMethodInterceptor
{
    /// <summary>
    /// Gets the type of the interceptor to apply.
    /// </summary>
    public Type InterceptorType => typeof(TInterceptor);

    /// <summary>
    /// Gets or sets the order in which this interceptor executes relative to
    /// other interceptors on the same target. Lower values execute first
    /// (outermost in the interceptor chain). Default is 0.
    /// </summary>
    public int Order { get; set; } = 0;
}
