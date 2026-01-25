namespace NexusLabs.Needlr;

/// <summary>
/// Specifies that the decorated class should be registered with Singleton lifetime.
/// This is the default lifetime in Needlr, so this attribute is only needed when
/// overriding a different default configuration.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class SingletonAttribute : Attribute
{
}
