namespace NexusLabs.Needlr.AgentFramework;

/// <summary>
/// Marks an interface for source generation of an <see cref="AsyncLocal{T}"/>-backed
/// implementation. The generator emits an <c>internal sealed class</c> that implements
/// the interface with proper scope nesting and dispose semantics.
/// </summary>
/// <remarks>
/// <para>
/// The interface must declare:
/// <list type="bullet">
///   <item>A nullable read-only property named <c>Current</c> (determines the value type).</item>
///   <item>A method returning <see cref="IDisposable"/> (the scope entry point).</item>
/// </list>
/// </para>
/// <para>
/// When <see cref="Mutable"/> is <see langword="true"/>, the generated implementation uses
/// a mutable holder object in the <see cref="AsyncLocal{T}"/> slot so that writes from child
/// async flows are visible to the parent scope (the pattern used by diagnostics accessors).
/// </para>
/// </remarks>
/// <example>
/// <code>
/// [AsyncLocalScoped]
/// public interface IMyContextAccessor
/// {
///     MyContext? Current { get; }
///     IDisposable BeginScope(MyContext value);
/// }
/// // Generator emits: internal sealed class MyContextAccessor : IMyContextAccessor { ... }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Interface, AllowMultiple = false, Inherited = false)]
public sealed class AsyncLocalScopedAttribute : Attribute
{
    /// <summary>
    /// When <see langword="true"/>, the generated implementation uses a mutable holder object
    /// so that values set from child async flows are visible to the parent scope.
    /// Default is <see langword="false"/> (simple <see cref="AsyncLocal{T}"/> with scope restore).
    /// </summary>
    public bool Mutable { get; init; }
}
