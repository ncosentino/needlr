using System;

namespace NexusLabs.Needlr.Generators;

/// <summary>
/// Specifies the order in which referenced assemblies should be loaded
/// during application startup for source-generated dependency injection.
/// </summary>
/// <remarks>
/// <para>
/// By default, Needlr auto-discovers all referenced assemblies with
/// <see cref="GenerateTypeRegistryAttribute"/> and loads them in alphabetical
/// order. Use this attribute to override the default ordering when specific
/// assemblies must be loaded before or after others.
/// </para>
/// <para>
/// Assembly names specified in <see cref="First"/> are loaded first, in order.
/// Assembly names specified in <see cref="Last"/> are loaded last, in order.
/// All other discovered assemblies are loaded alphabetically between them.
/// </para>
/// <para>
/// This attribute is completely optional. If not specified, all assemblies
/// are loaded in alphabetical order by assembly name.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // Load logging first, health checks last
/// [assembly: GenerateTypeRegistry]
/// [assembly: NeedlrAssemblyOrder(
///     First = new[] { "MyApp.Features.Logging", "MyApp.Features.Configuration" },
///     Last = new[] { "MyApp.Features.Health" })]
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false)]
public sealed class NeedlrAssemblyOrderAttribute : Attribute
{
    /// <summary>
    /// Gets or sets the assembly names that should be loaded first, in order.
    /// </summary>
    /// <remarks>
    /// These assemblies are loaded before all other auto-discovered assemblies.
    /// The order within this array is preserved.
    /// </remarks>
    public string[]? First { get; set; }

    /// <summary>
    /// Gets or sets the assembly names that should be loaded last, in order.
    /// </summary>
    /// <remarks>
    /// These assemblies are loaded after all other auto-discovered assemblies.
    /// The order within this array is preserved.
    /// </remarks>
    public string[]? Last { get; set; }
}
