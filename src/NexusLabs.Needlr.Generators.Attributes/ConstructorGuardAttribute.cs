using System;

namespace NexusLabs.Needlr.Generators;

/// <summary>
/// Requests a constructor guard clause for a field participating in generated
/// constructor generation via <see cref="GenerateConstructorAttribute"/>.
/// </summary>
/// <remarks>
/// <para>
/// Applying this attribute with any kind other than <see cref="ConstructorGuardKind.None"/>,
/// or with a custom guard type, is itself a positive trigger: it enables constructor
/// generation with <see cref="ConstructorNullGuardMode.None"/> even if the containing
/// class does not carry <see cref="GenerateConstructorAttribute"/>. In that case every
/// other eligible field still becomes a constructor parameter, but only fields with
/// their own explicit guard receive one.
/// </para>
/// <para>
/// A custom guard type must expose an accessible <see langword="static"/> method
/// compatible with <c>void Validate(T value, string parameterName)</c>, where
/// <c>T</c> is compatible with the field's type. The generator emits a
/// direct, fully-qualified call to that method -- never a reflection-based invocation.
/// </para>
/// <para>
/// This attribute may be applied more than once to the same field to request several
/// guards additively. The class-level default guard (when applicable) is always
/// composed first, followed by every explicit <see cref="ConstructorGuardAttribute"/>
/// in source declaration order. Identical effective guard calls -- the same built-in
/// <see cref="ConstructorGuardKind"/>, or the same resolved custom guard type and
/// method -- are emitted only once even if requested more than once.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// public partial class TenantService
/// {
///     [ConstructorGuard(ConstructorGuardKind.NotNullOrWhiteSpace)]
///     private readonly string _tenantName;
/// }
/// </code>
/// <code>
/// public partial class OrderService
/// {
///     [ConstructorGuard(typeof(CollectionNotEmptyGuard))]
///     private readonly IReadOnlyCollection&lt;Order&gt; _orders;
/// }
///
/// // Generated call: CollectionNotEmptyGuard.Validate(orders, nameof(orders));
/// </code>
/// <code>
/// public partial class RetryPolicy
/// {
///     [ConstructorGuard(typeof(NumberGuards), nameof(NumberGuards.ValidatePositive))]
///     private readonly int _retryCount;
/// }
///
/// // Generated call: NumberGuards.ValidatePositive(retryCount, nameof(retryCount));
/// </code>
/// <code>
/// public partial class OrderService
/// {
///     [ConstructorGuard(ConstructorGuardKind.NotNullOrWhiteSpace)]
///     [ConstructorGuard(typeof(OrderIdFormatGuard))]
///     private readonly string _orderId;
/// }
///
/// // Generated calls, in declaration order:
/// // global::System.ArgumentException.ThrowIfNullOrWhiteSpace(orderId);
/// // OrderIdFormatGuard.Validate(orderId, nameof(orderId));
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Field, Inherited = false, AllowMultiple = true)]
public sealed class ConstructorGuardAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ConstructorGuardAttribute"/> class
    /// requesting a built-in guard kind.
    /// </summary>
    /// <param name="kind">The built-in guard clause to emit for this field.</param>
    public ConstructorGuardAttribute(ConstructorGuardKind kind)
    {
        Kind = kind;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ConstructorGuardAttribute"/> class
    /// requesting a custom guard type using the conventional <c>Validate</c> method.
    /// </summary>
    /// <param name="guardType">
    /// A type exposing an accessible <see langword="static"/> method named
    /// <c>Validate</c>, compatible with <c>void Validate(T value, string parameterName)</c>.
    /// </param>
    public ConstructorGuardAttribute(Type guardType)
    {
        GuardType = guardType;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ConstructorGuardAttribute"/> class
    /// requesting a custom guard type and an explicit validation method name.
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
    public ConstructorGuardAttribute(Type guardType, string methodName)
    {
        GuardType = guardType;
        MethodName = methodName;
    }

    /// <summary>
    /// Gets the requested built-in guard kind, or <see cref="ConstructorGuardKind.None"/>
    /// when a custom <see cref="GuardType"/> was supplied instead.
    /// </summary>
    public ConstructorGuardKind Kind { get; }

    /// <summary>
    /// Gets the custom guard type supplied to this attribute, or
    /// <see langword="null"/> when a built-in <see cref="Kind"/> was supplied instead.
    /// </summary>
    public Type? GuardType { get; }

    /// <summary>
    /// Gets the explicit validation method name supplied to this attribute, or
    /// <see langword="null"/> when the conventional <c>Validate</c> method name applies.
    /// </summary>
    public string? MethodName { get; }
}
