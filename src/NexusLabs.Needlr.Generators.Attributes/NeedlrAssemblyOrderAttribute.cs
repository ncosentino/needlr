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
/// You can use <see cref="Preset"/> for common ordering patterns, or use
/// <see cref="First"/> and <see cref="Last"/> for explicit control.
/// </para>
/// <para>
/// This attribute is completely optional. If not specified, all assemblies
/// are loaded in alphabetical order by assembly name.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // Use a preset ordering (tests loaded last)
/// [assembly: GenerateTypeRegistry]
/// [assembly: NeedlrAssemblyOrder(Preset = AssemblyOrderPreset.TestsLast)]
/// 
/// // Or use explicit ordering
/// [assembly: NeedlrAssemblyOrder(
///     First = new[] { "MyApp.Features.Logging" },
///     Last = new[] { "MyApp.Features.Health" })]
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false)]
public sealed class NeedlrAssemblyOrderAttribute : Attribute
{
    /// <summary>
    /// Gets or sets the preset ordering to use.
    /// When set, this takes precedence over <see cref="First"/> and <see cref="Last"/>.
    /// </summary>
    public AssemblyOrderPreset Preset { get; set; } = AssemblyOrderPreset.None;

    /// <summary>
    /// Gets or sets the assembly names that should be loaded first, in order.
    /// </summary>
    /// <remarks>
    /// These assemblies are loaded before all other auto-discovered assemblies.
    /// The order within this array is preserved.
    /// Ignored if <see cref="Preset"/> is set to a value other than <see cref="AssemblyOrderPreset.None"/>.
    /// </remarks>
    public string[]? First { get; set; }

    /// <summary>
    /// Gets or sets the assembly names that should be loaded last, in order.
    /// </summary>
    /// <remarks>
    /// These assemblies are loaded after all other auto-discovered assemblies.
    /// The order within this array is preserved.
    /// Ignored if <see cref="Preset"/> is set to a value other than <see cref="AssemblyOrderPreset.None"/>.
    /// </remarks>
    public string[]? Last { get; set; }
}

/// <summary>
/// Predefined assembly ordering presets for source generation.
/// These match the presets available in <c>AssemblyOrder</c> for reflection scenarios.
/// </summary>
public enum AssemblyOrderPreset
{
    /// <summary>
    /// No preset - use <see cref="NeedlrAssemblyOrderAttribute.First"/> and 
    /// <see cref="NeedlrAssemblyOrderAttribute.Last"/> for explicit ordering,
    /// or alphabetical if neither is specified.
    /// </summary>
    None = 0,

    /// <summary>
    /// Non-test assemblies first, test assemblies last.
    /// Equivalent to <c>AssemblyOrder.TestsLast()</c> in reflection scenarios.
    /// </summary>
    TestsLast = 1,

    /// <summary>
    /// Alphabetical ordering by assembly name.
    /// Equivalent to <c>AssemblyOrder.Alphabetical()</c> in reflection scenarios.
    /// </summary>
    Alphabetical = 2
}
