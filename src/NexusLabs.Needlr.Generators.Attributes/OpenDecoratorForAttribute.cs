using System;

namespace NexusLabs.Needlr.Generators;

/// <summary>
/// Marks a generic class as a decorator for all closed implementations of an open generic interface.
/// This is a source-generation only feature - the generator discovers all closed implementations
/// at compile time and emits decorator registrations for each.
/// </summary>
/// <remarks>
/// <para>
/// Use this attribute when you want to apply a single decorator implementation to ALL
/// closed types of an open generic interface. For example, to add logging to every
/// <c>IHandler&lt;T&gt;</c> implementation.
/// </para>
/// <para>
/// The decorated class must:
/// <list type="bullet">
/// <item><description>Be an open generic class with matching type parameter arity</description></item>
/// <item><description>Implement the open generic interface specified</description></item>
/// <item><description>Accept the open generic interface as a constructor parameter (to wrap the inner service)</description></item>
/// </list>
/// </para>
/// <para>
/// At compile time, the source generator:
/// <list type="number">
/// <item><description>Discovers all closed implementations of the open generic interface</description></item>
/// <item><description>Emits <c>AddDecorator&lt;TService, TDecorator&gt;</c> for each closed type</description></item>
/// </list>
/// </para>
/// </remarks>
/// <example>
/// <code>
/// public interface IHandler&lt;T&gt;
/// {
///     Task HandleAsync(T message);
/// }
/// 
/// // Concrete handlers
/// public class OrderHandler : IHandler&lt;Order&gt; { ... }
/// public class PaymentHandler : IHandler&lt;Payment&gt; { ... }
/// 
/// // Open generic decorator - applies to ALL IHandler&lt;T&gt; implementations
/// [OpenDecoratorFor(typeof(IHandler&lt;&gt;))]
/// public class LoggingDecorator&lt;T&gt; : IHandler&lt;T&gt;
/// {
///     private readonly IHandler&lt;T&gt; _inner;
///     private readonly ILogger&lt;LoggingDecorator&lt;T&gt;&gt; _logger;
///     
///     public LoggingDecorator(IHandler&lt;T&gt; inner, ILogger&lt;LoggingDecorator&lt;T&gt;&gt; logger)
///     {
///         _inner = inner;
///         _logger = logger;
///     }
///     
///     public async Task HandleAsync(T message)
///     {
///         _logger.LogInformation("Handling {Type}", typeof(T).Name);
///         await _inner.HandleAsync(message);
///         _logger.LogInformation("Handled {Type}", typeof(T).Name);
///     }
/// }
/// 
/// // Generator emits:
/// // services.AddDecorator&lt;IHandler&lt;Order&gt;, LoggingDecorator&lt;Order&gt;&gt;();
/// // services.AddDecorator&lt;IHandler&lt;Payment&gt;, LoggingDecorator&lt;Payment&gt;&gt;();
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = true)]
public sealed class OpenDecoratorForAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="OpenDecoratorForAttribute"/> class.
    /// </summary>
    /// <param name="openGenericServiceType">
    /// The open generic interface type to decorate (e.g., <c>typeof(IHandler&lt;&gt;)</c>).
    /// Must be an open generic interface.
    /// </param>
    public OpenDecoratorForAttribute(Type openGenericServiceType)
    {
        OpenGenericServiceType = openGenericServiceType;
    }

    /// <summary>
    /// Gets the open generic interface type that this decorator wraps.
    /// </summary>
    public Type OpenGenericServiceType { get; }

    /// <summary>
    /// Gets or sets the order in which this decorator is applied relative to
    /// other decorators for the same service. Lower values are applied first
    /// (closer to the original implementation). Default is 0.
    /// </summary>
    /// <remarks>
    /// <para>
    /// When multiple decorators exist for the same open generic interface,
    /// they are applied in order from lowest to highest Order value.
    /// </para>
    /// </remarks>
    public int Order { get; set; } = 0;
}
