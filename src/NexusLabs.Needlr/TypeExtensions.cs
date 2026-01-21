namespace NexusLabs.Needlr;

/// <summary>
/// Extension methods for <see cref="Type"/> to provide additional type inspection capabilities.
/// </summary>
public static class TypeExtensions
{
    /// <summary>
    /// Determines whether the specified type is a static class.
    /// </summary>
    /// <param name="type">The type to check.</param>
    /// <returns>True if the type is static (abstract and sealed); otherwise, false.</returns>
    public static bool IsStatic(this Type type)
    {
        return type.IsAbstract && type.IsSealed;
    }
}