namespace NexusLabs.Needlr;

/// <summary>
/// Specifies the execution order for a plugin. Lower values execute first.
/// Plugins without this attribute default to Order = 0.
/// </summary>
/// <remarks>
/// <para>
/// Use this attribute to control the order in which plugins are executed.
/// This is useful when plugins have dependencies on each other or when
/// certain plugins must run before or after others.
/// </para>
/// <para>
/// Example usage:
/// <code>
/// // Infrastructure plugins run first (negative order)
/// [PluginOrder(-100)]
/// public class DatabaseMigrationPlugin : IServiceCollectionPlugin { }
/// 
/// // Default order (0) - no attribute needed
/// public class BusinessLogicPlugin : IServiceCollectionPlugin { }
/// 
/// // Validation plugins run last (positive order)
/// [PluginOrder(100)]
/// public class ValidationPlugin : IServiceCollectionPlugin { }
/// </code>
/// </para>
/// <para>
/// When multiple plugins have the same order, they are sorted alphabetically
/// by their fully qualified type name to ensure deterministic execution order.
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class PluginOrderAttribute : Attribute
{
    /// <summary>
    /// Gets the execution order. Lower values execute first.
    /// </summary>
    public int Order { get; }

    /// <summary>
    /// Initializes a new instance of <see cref="PluginOrderAttribute"/> with the specified order.
    /// </summary>
    /// <param name="order">
    /// The execution order. Lower values execute first. 
    /// Negative values run before default (0), positive values run after.
    /// </param>
    public PluginOrderAttribute(int order) => Order = order;
}
