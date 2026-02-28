namespace NexusLabs.Needlr;

/// <summary>
/// Specifies that the decorated class should be registered with <b>Singleton</b> lifetime.
/// Singleton services are created once for the lifetime of the application and reused for every request.
/// </summary>
/// <remarks>
/// <para>
/// <b>Singleton is the default lifetime in Needlr.</b> You only need to apply this attribute explicitly
/// when you want to document intent clearly, or when overriding a different default set in your
/// Needlr configuration.
/// </para>
/// <para>
/// Be careful with Singleton services that depend on Scoped or Transient services — resolving a
/// shorter-lived dependency from a Singleton creates a "captive dependency" and can cause bugs.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// [Singleton]
/// public class ConfigurationCache : IConfigurationCache
/// {
///     // Created once for the application lifetime
/// }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class SingletonAttribute : Attribute
{
}
