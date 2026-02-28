namespace NexusLabs.Needlr;

/// <summary>
/// Specifies that the decorated class should be registered with <b>Scoped</b> lifetime.
/// Scoped services are created once per scope — for example, once per HTTP request in ASP.NET Core.
/// </summary>
/// <remarks>
/// <para>
/// Use <c>[Scoped]</c> when a service must share state across multiple calls within the same
/// logical operation (e.g., a request), but must not be shared across operations.
/// </para>
/// <para>
/// Without this attribute, Needlr registers classes as <b>Singleton</b> by default.
/// Apply <c>[Scoped]</c> to override that default.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// [Scoped]
/// public class OrderRepository : IOrderRepository
/// {
///     // Created once per HTTP request / DI scope
/// }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class ScopedAttribute : Attribute
{
}
