using System;

namespace NexusLabs.Needlr.Generators;

/// <summary>
/// Includes an additional record property as a parameter in a generated public
/// forwarding constructor overload.
/// </summary>
/// <remarks>
/// <para>
/// This marker is the positive trigger for record constructor-overload generation.
/// Apply it to one or more assignable instance properties declared directly by a
/// top-level partial positional <see langword="record class"/>. The generated overload
/// accepts every positional primary-constructor parameter first, followed by every
/// marked property in deterministic source order.
/// </para>
/// <para>
/// The overload forwards the positional parameters to the primary constructor, emits
/// any <see cref="ConstructorGuardAttribute"/> guards applied to marked properties,
/// and then assigns those properties. It does not change the primary constructor,
/// copy constructor, object-initializer, serializer, or <c>with</c>-expression paths.
/// Those paths can still assign values that bypass the generated overload's guards.
/// </para>
/// <para>
/// Record structs, body-only records, file-local records, nested or inherited records,
/// ordinary classes, positional properties, static properties, indexers, get-only or
/// required properties, and property types less accessible than the generated public
/// constructor are unsupported and produce compile-time diagnostics.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// public partial record PreparedRequest(string Query, string Tenant)
/// {
///     [RecordConstructorOverloadParameter]
///     [ConstructorGuard(ConstructorGuardKind.NotNull)]
///     public PreparedScope? PreparedScope { get; init; }
/// }
///
/// // Generated:
/// // public PreparedRequest(
/// //     string Query,
/// //     string Tenant,
/// //     PreparedScope PreparedScope)
/// //     : this(Query, Tenant)
/// // {
/// //     ArgumentNullException.ThrowIfNull(PreparedScope);
/// //     this.PreparedScope = PreparedScope;
/// // }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Property, Inherited = false, AllowMultiple = false)]
public sealed class RecordConstructorOverloadParameterAttribute : Attribute
{
}
