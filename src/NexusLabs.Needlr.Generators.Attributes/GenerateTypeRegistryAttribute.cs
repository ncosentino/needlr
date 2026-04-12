using System;

namespace NexusLabs.Needlr.Generators;

/// <summary>
/// Marks an assembly for compile-time type registry generation.
/// The source generator will scan all referenced assemblies and generate
/// a TypeRegistry class containing all injectable types.
/// </summary>
/// <remarks>
/// <para>
/// This attribute triggers the source generator to analyze all types
/// in referenced assemblies at compile time, eliminating the need for
/// runtime reflection-based type discovery.
/// </para>
/// <para>
/// Use <see cref="IncludeNamespacePrefixes"/> to filter which types
/// are included in the generated registry. This is similar to the
/// <c>MatchingAssemblies()</c> method in the reflection-based approach.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // Include all types from NexusLabs and MyCompany namespaces
/// [assembly: GenerateTypeRegistry(IncludeNamespacePrefixes = new[] { "NexusLabs", "MyCompany" })]
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false)]
public sealed class GenerateTypeRegistryAttribute : Attribute
{
    /// <summary>
    /// Gets or sets namespace prefix filters. Only types in namespaces starting with
    /// these prefixes will be included in the generated registry.
    /// </summary>
    /// <remarks>
    /// This is analogous to the <c>MatchingAssemblies()</c> configuration in the
    /// reflection-based approach. If null or empty, all namespaces are included.
    /// When both <see cref="IncludeNamespacePrefixes"/> and <see cref="ExcludeNamespacePrefixes"/>
    /// are set, inclusion runs first, then exclusion filters out matches.
    /// </remarks>
    public string[]? IncludeNamespacePrefixes { get; set; }

    /// <summary>
    /// Gets or sets namespace prefixes to exclude from the generated registry.
    /// Types whose namespace starts with any of these prefixes will be skipped,
    /// even if they match <see cref="IncludeNamespacePrefixes"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Use this to prevent scanning framework types from libraries like Avalonia, MAUI,
    /// or other UI frameworks that expose many public types Needlr would otherwise try
    /// to register. Exclusion is applied after inclusion — if a type matches both an
    /// include prefix and an exclude prefix, it is excluded.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Include MyApp types but exclude Avalonia framework types
    /// [assembly: GenerateTypeRegistry(
    ///     IncludeNamespacePrefixes = new[] { "MyApp" },
    ///     ExcludeNamespacePrefixes = new[] { "Avalonia" })]
    ///
    /// // Include everything except Avalonia
    /// [assembly: GenerateTypeRegistry(
    ///     ExcludeNamespacePrefixes = new[] { "Avalonia" })]
    /// </code>
    /// </example>
    public string[]? ExcludeNamespacePrefixes { get; set; }

    /// <summary>
    /// Gets or sets whether to include types from the current assembly
    /// in addition to referenced assemblies.
    /// </summary>
    /// <remarks>
    /// Defaults to <c>true</c>. Set to <c>false</c> to only include types
    /// from project references.
    /// </remarks>
    public bool IncludeSelf { get; set; } = true;
}
