namespace NexusLabs.Needlr;

/// <summary>
/// Specifies that the decorated class should be registered with <b>Transient</b> lifetime.
/// Transient services are created fresh every time they are resolved from the container.
/// </summary>
/// <remarks>
/// <para>
/// Use <c>[Transient]</c> for lightweight, stateless services that are cheap to construct
/// and must not accumulate state across calls.
/// </para>
/// <para>
/// Without this attribute, Needlr registers classes as <b>Singleton</b> by default.
/// Apply <c>[Transient]</c> to override that default.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// [Transient]
/// public class EmailMessageBuilder : IEmailMessageBuilder
/// {
///     // New instance every time IEmailMessageBuilder is resolved
/// }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class TransientAttribute : Attribute
{
}
