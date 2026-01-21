namespace NexusLabs.Needlr;

/// <summary>
/// Marks a class or interface to be excluded from automatic dependency injection registration.
/// Types decorated with this attribute will be discovered but not registered in the service collection.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface, Inherited = true, AllowMultiple = false)]
public sealed class DoNotInjectAttribute : Attribute
{
}
