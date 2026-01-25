namespace NexusLabs.Needlr;

/// <summary>
/// Specifies that the decorated class should be registered with Scoped lifetime.
/// Scoped services are created once per scope (e.g., per HTTP request in ASP.NET Core).
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class ScopedAttribute : Attribute
{
}
