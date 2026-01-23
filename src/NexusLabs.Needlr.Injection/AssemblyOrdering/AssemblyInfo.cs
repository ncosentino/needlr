using System.Reflection;

namespace NexusLabs.Needlr.Injection.AssemblyOrdering;

/// <summary>
/// Provides a simplified view of assembly information for ordering expressions.
/// This abstraction works for both reflection (runtime Assembly) and source-gen (compile-time info).
/// </summary>
public sealed class AssemblyInfo
{
    /// <summary>
    /// The assembly name (without extension).
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// The full file path/location of the assembly (if available).
    /// </summary>
    public string Location { get; }

    /// <summary>
    /// Creates an AssemblyInfo from a runtime Assembly.
    /// </summary>
    public static AssemblyInfo FromAssembly(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        return new AssemblyInfo(
            assembly.GetName().Name ?? string.Empty,
            assembly.Location);
    }

    /// <summary>
    /// Creates an AssemblyInfo from string values (for source-gen scenarios).
    /// </summary>
    public static AssemblyInfo FromStrings(string name, string location = "")
    {
        return new AssemblyInfo(name, location);
    }

    private AssemblyInfo(string name, string location)
    {
        Name = name ?? string.Empty;
        Location = location ?? string.Empty;
    }
}
