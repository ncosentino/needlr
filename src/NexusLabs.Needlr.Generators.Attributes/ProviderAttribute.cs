using System;

namespace NexusLabs.Needlr.Generators;

/// <summary>
/// Marks an interface or partial class as a Provider - a strongly-typed service locator.
/// </summary>
/// <remarks>
/// <para>
/// A Provider is a compile-time generated class that provides strongly-typed access to
/// services registered in the dependency injection container. Unlike using
/// <c>IServiceProvider.GetService&lt;T&gt;()</c> directly, Providers offer:
/// </para>
/// <list type="bullet">
/// <item><description>Compile-time verification that required services are registered</description></item>
/// <item><description>IntelliSense and IDE support for available services</description></item>
/// <item><description>Easy mocking in unit tests</description></item>
/// </list>
/// <para>
/// <b>Providers are always registered as Singletons.</b> All service properties are resolved
/// via constructor injection at Provider construction time (fail-fast). For creating new
/// instances on demand, use the <see cref="Factories"/> parameter to generate factory properties.
/// </para>
/// <para>
/// <b>Usage modes:</b>
/// </para>
/// <list type="number">
/// <item>
/// <description>
/// <b>Interface definition:</b> Apply to an interface with get-only properties.
/// The generator creates an implementing class.
/// <code>
/// [Provider]
/// public interface IOrderServicesProvider
/// {
///     IOrderRepository Repository { get; }
///     IOrderValidator Validator { get; }
/// }
/// // Generates: OrderServicesProvider class
/// </code>
/// </description>
/// </item>
/// <item>
/// <description>
/// <b>Shorthand class:</b> Apply to a partial class with type parameters.
/// The generator creates both an interface and the implementation.
/// <code>
/// [Provider(typeof(IOrderRepository), typeof(IOrderValidator))]
/// public partial class OrderDependenciesProvider { }
/// // Generates: IOrderDependenciesProvider interface + implementation
/// </code>
/// </description>
/// </item>
/// </list>
/// </remarks>
[AttributeUsage(AttributeTargets.Interface | AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class ProviderAttribute : Attribute
{
    /// <summary>
    /// Creates a Provider attribute for an interface definition.
    /// Define service properties directly on the interface.
    /// </summary>
    public ProviderAttribute()
    {
    }

    /// <summary>
    /// Creates a Provider with the specified required service types.
    /// Use on a partial class to auto-generate interface and implementation.
    /// </summary>
    /// <param name="requiredServices">
    /// Service types that must be registered in the container.
    /// Property names are derived from type names (e.g., IOrderRepository → OrderRepository).
    /// </param>
    public ProviderAttribute(params Type[] requiredServices)
    {
        Required = requiredServices;
    }

    /// <summary>
    /// Service types that must be registered. Resolution failure throws at startup.
    /// </summary>
    /// <remarks>
    /// Properties for these services use <c>GetRequiredService&lt;T&gt;()</c>.
    /// If any service is not registered, the application fails to start.
    /// </remarks>
    public Type[]? Required { get; set; }

    /// <summary>
    /// Service types that may not be registered. Properties are nullable.
    /// </summary>
    /// <remarks>
    /// Properties for these services use <c>GetService&lt;T&gt;()</c> and return null
    /// if the service is not registered.
    /// </remarks>
    public Type[]? Optional { get; set; }

    /// <summary>
    /// Service types to resolve as <c>IEnumerable&lt;T&gt;</c>.
    /// </summary>
    /// <remarks>
    /// Properties for these services use <c>GetServices&lt;T&gt;()</c> and return
    /// all registered implementations.
    /// </remarks>
    public Type[]? Collections { get; set; }

    /// <summary>
    /// Types to generate factories for, enabling creation of new instances.
    /// </summary>
    /// <remarks>
    /// <para>
    /// For each type specified:
    /// </para>
    /// <list type="bullet">
    /// <item><description>
    /// If the type has <c>[GenerateFactory]</c>, the existing factory is used.
    /// </description></item>
    /// <item><description>
    /// Otherwise, a factory (<c>IXxxFactory</c>) is generated automatically.
    /// </description></item>
    /// </list>
    /// <para>
    /// This allows the Provider to create new instances while remaining a Singleton.
    /// </para>
    /// </remarks>
    public Type[]? Factories { get; set; }
}
