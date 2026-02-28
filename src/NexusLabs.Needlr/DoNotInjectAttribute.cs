namespace NexusLabs.Needlr;

/// <summary>
/// Prevents a class or interface from being injected as a dependency by Needlr.
/// Types decorated with this attribute are discovered but excluded from service collection registration.
/// </summary>
/// <remarks>
/// <para>
/// Use <c>[DoNotInject]</c> when a type should never appear as a resolvable service — for example,
/// infrastructure base classes, marker interfaces, or types that are only used as generic constraints.
/// </para>
/// <para>
/// Unlike <see cref="DoNotAutoRegisterAttribute"/>, which signals "register me some other way",
/// <c>[DoNotInject]</c> signals "this type is not a DI service at all".
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // This interface is used as a constraint but should never be resolved from the container
/// [DoNotInject]
/// public interface IInternalMarker
/// {
/// }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface, Inherited = true, AllowMultiple = false)]
public sealed class DoNotInjectAttribute : Attribute
{
}
