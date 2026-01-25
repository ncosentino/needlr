namespace NexusLabs.Needlr;

/// <summary>
/// Marks a class for keyed service registration. The type will be registered
/// with the specified service key in addition to its normal registration.
/// </summary>
/// <remarks>
/// <para>
/// Use this attribute when you want to register multiple implementations of
/// the same interface with different keys, allowing consumers to resolve
/// specific implementations using <c>[FromKeyedServices("key")]</c>.
/// </para>
/// <para>
/// Example:
/// <code>
/// [Keyed("stripe")]
/// public class StripeProcessor : IPaymentProcessor { }
/// 
/// [Keyed("paypal")]
/// public class PayPalProcessor : IPaymentProcessor { }
/// 
/// // Consumer resolves specific implementation:
/// public class OrderService(
///     [FromKeyedServices("stripe")] IPaymentProcessor processor) { }
/// </code>
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = true)]
public sealed class KeyedAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="KeyedAttribute"/> class.
    /// </summary>
    /// <param name="key">The service key for this registration.</param>
    public KeyedAttribute(string key)
    {
        Key = key ?? throw new ArgumentNullException(nameof(key));
    }

    /// <summary>
    /// Gets the service key for this registration.
    /// </summary>
    public string Key { get; }
}
