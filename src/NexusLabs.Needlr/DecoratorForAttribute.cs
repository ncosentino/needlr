namespace NexusLabs.Needlr;

/// <summary>
/// Marks a class as a decorator for the specified service type. When discovered
/// by Needlr (via source generation or reflection), the decorator will be 
/// automatically wired using <see cref="ServiceCollectionExtensions.AddDecorator{TService, TDecorator}"/>.
/// </summary>
/// <typeparam name="TService">The service type (interface or class) that this class decorates.</typeparam>
/// <remarks>
/// <para>
/// The decorated class must implement <typeparamref name="TService"/> and accept
/// an instance of <typeparamref name="TService"/> in its constructor.
/// </para>
/// <para>
/// When multiple decorators exist for the same service, use the <see cref="Order"/> 
/// property to control the decoration order. Lower values are applied first (closer
/// to the original service), higher values wrap outer layers.
/// </para>
/// <para>
/// Using this attribute implicitly excludes the type from normal interface 
/// registration (equivalent to applying <see cref="DoNotAutoRegisterAttribute"/>
/// for the decorated interface).
/// </para>
/// </remarks>
/// <example>
/// <code>
/// public interface IMyService
/// {
///     string GetValue();
/// }
/// 
/// public class MyService : IMyService
/// {
///     public string GetValue() => "Original";
/// }
/// 
/// [DecoratorFor&lt;IMyService&gt;(Order = 1)]
/// public class LoggingDecorator : IMyService
/// {
///     private readonly IMyService _inner;
///     
///     public LoggingDecorator(IMyService inner) => _inner = inner;
///     
///     public string GetValue()
///     {
///         Console.WriteLine("Before");
///         var result = _inner.GetValue();
///         Console.WriteLine("After");
///         return result;
///     }
/// }
/// 
/// [DecoratorFor&lt;IMyService&gt;(Order = 2)]
/// public class CachingDecorator : IMyService
/// {
///     private readonly IMyService _inner;
///     private string? _cached;
///     
///     public CachingDecorator(IMyService inner) => _inner = inner;
///     
///     public string GetValue() => _cached ??= _inner.GetValue();
/// }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = true)]
public sealed class DecoratorForAttribute<TService> : Attribute
    where TService : class
{
    /// <summary>
    /// Gets or sets the order in which this decorator is applied relative to
    /// other decorators for the same service. Lower values are applied first
    /// (closer to the original implementation). Default is 0.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Example with Order values:
    /// <list type="bullet">
    /// <item>Original service: MyService</item>
    /// <item>Order = 1: LoggingDecorator wraps MyService</item>
    /// <item>Order = 2: CachingDecorator wraps LoggingDecorator</item>
    /// </list>
    /// Resolving IMyService returns: CachingDecorator → LoggingDecorator → MyService
    /// </para>
    /// </remarks>
    public int Order { get; set; } = 0;

    /// <summary>
    /// Gets the service type that this decorator wraps.
    /// </summary>
    public Type ServiceType => typeof(TService);
}
