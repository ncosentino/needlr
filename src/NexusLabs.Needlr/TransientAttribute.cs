namespace NexusLabs.Needlr;

/// <summary>
/// Specifies that the decorated class should be registered with Transient lifetime.
/// Transient services are created each time they are requested.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class TransientAttribute : Attribute
{
}
