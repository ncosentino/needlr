namespace NexusLabs.Needlr.Injection.AssemblyOrdering;

/// <summary>
/// Factory for creating assembly ordering configurations.
/// </summary>
public static class AssemblyOrder
{
    /// <summary>
    /// Creates a new assembly order builder.
    /// </summary>
    /// <returns>A new builder instance.</returns>
    public static AssemblyOrderBuilder Create() => new();

    /// <summary>
    /// Creates an ordering configuration from a builder action.
    /// </summary>
    /// <param name="configure">Action to configure the ordering rules.</param>
    /// <returns>The configured builder.</returns>
    public static AssemblyOrderBuilder Create(Action<AssemblyOrderBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        var builder = new AssemblyOrderBuilder();
        configure(builder);
        return builder;
    }

    /// <summary>
    /// Creates a preset ordering: libraries first, then executables, tests last.
    /// Equivalent to the old UseLibTestEntrySorting().
    /// </summary>
    /// <returns>A builder configured with lib-test-entry ordering.</returns>
    public static AssemblyOrderBuilder LibTestEntry() =>
        Create()
            .By(a => a.Location.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) 
                     && !a.Name.Contains("Tests", StringComparison.OrdinalIgnoreCase))
            .ThenBy(a => a.Location.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            .ThenBy(a => a.Name.Contains("Tests", StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Creates a preset ordering: alphabetical by name.
    /// </summary>
    /// <returns>A builder configured with alphabetical ordering.</returns>
    public static AssemblyOrderBuilder Alphabetical() =>
        Create()
            .By(a => true); // All assemblies match, sorted alphabetically within tier
}
