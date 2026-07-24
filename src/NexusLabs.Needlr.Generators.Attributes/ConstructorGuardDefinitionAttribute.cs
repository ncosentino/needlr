using System;

namespace NexusLabs.Needlr.Generators;

/// <summary>
/// Declares that an application-defined attribute type is an alias for a constructor
/// guard, so it can be applied to fields or participating record properties instead of
/// <see cref="ConstructorGuardAttribute"/>.
/// </summary>
/// <remarks>
/// <para>
/// Apply this meta-attribute to your own <see cref="Attribute"/>-derived field or
/// property attribute type. When the generator finds your attribute applied to a
/// supported member, it resolves the guard from this meta-attribute and treats the
/// member exactly as if
/// <see cref="ConstructorGuardAttribute"/> had been applied directly with the same
/// guard type. A positive alias on an eligible field triggers field-based constructor
/// generation; a property alias is emitted only when the property also carries
/// <see cref="RecordConstructorOverloadParameterAttribute"/>.
/// </para>
/// <para>
/// This works whether your alias attribute type is declared in the same project as
/// the field that uses it, or in a referenced assembly.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// [ConstructorGuardDefinition(typeof(CollectionNotEmptyGuard))]
/// [AttributeUsage(AttributeTargets.Field)]
/// public sealed class CollectionNotEmptyAttribute : Attribute
/// {
/// }
///
/// public partial class OrderService
/// {
///     [CollectionNotEmpty]
///     private readonly IReadOnlyCollection&lt;Order&gt; _orders;
/// }
///
/// // Generated call: CollectionNotEmptyGuard.Validate(orders, nameof(orders));
/// </code>
/// <code>
/// [ConstructorGuardDefinition(typeof(ScopeGuard))]
/// [AttributeUsage(AttributeTargets.Property)]
/// public sealed class ValidScopeAttribute : Attribute
/// {
/// }
///
/// public partial record PreparedRequest(string Query)
/// {
///     [RecordConstructorOverloadParameter]
///     [ValidScope]
///     public PreparedScope? Scope { get; init; }
/// }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class ConstructorGuardDefinitionAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ConstructorGuardDefinitionAttribute"/>
    /// class for a custom guard type using the conventional <c>Validate</c> method.
    /// </summary>
    /// <param name="guardType">
    /// A type exposing an accessible <see langword="static"/> method named
    /// <c>Validate</c>, compatible with <c>void Validate(T value, string parameterName)</c>.
    /// </param>
    public ConstructorGuardDefinitionAttribute(Type guardType)
    {
        GuardType = guardType;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ConstructorGuardDefinitionAttribute"/>
    /// class for a custom guard type and an explicit validation method name.
    /// </summary>
    /// <param name="guardType">
    /// A type exposing an accessible <see langword="static"/> method matching
    /// <paramref name="methodName"/>, compatible with
    /// <c>void Validate(T value, string parameterName)</c>.
    /// </param>
    /// <param name="methodName">
    /// The name of the static validation method to call, typically supplied via
    /// <see langword="nameof"/>.
    /// </param>
    public ConstructorGuardDefinitionAttribute(Type guardType, string methodName)
    {
        GuardType = guardType;
        MethodName = methodName;
    }

    /// <summary>
    /// Gets the custom guard type associated with the decorated alias attribute.
    /// </summary>
    public Type GuardType { get; }

    /// <summary>
    /// Gets the explicit validation method name, or <see langword="null"/> when the
    /// conventional <c>Validate</c> method name applies.
    /// </summary>
    public string? MethodName { get; }
}
