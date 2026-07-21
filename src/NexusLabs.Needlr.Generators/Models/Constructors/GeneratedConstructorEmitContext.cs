using System;

namespace NexusLabs.Needlr.Generators.Models;

/// <summary>
/// The stable, scalar emission context shared by every generated-constructor model in
/// the compilation: the current assembly name and the configured breadcrumb verbosity.
/// </summary>
/// <remarks>
/// Deliberately holds only cheap, independently-cacheable scalar values rather than the
/// <see cref="Microsoft.CodeAnalysis.Compilation"/> or
/// <see cref="Microsoft.CodeAnalysis.Diagnostics.AnalyzerConfigOptionsProvider"/> objects
/// they were derived from. Combining a per-type model with the entire
/// <c>Compilation</c> would invalidate every generated constructor whenever anything in
/// the compilation changed (the exact bug this model exists to avoid); combining with
/// this instead lets the incremental pipeline reuse a cached emission context across
/// edits that don't touch the assembly name or breadcrumb MSBuild property.
/// </remarks>
internal readonly struct GeneratedConstructorEmitContext : IEquatable<GeneratedConstructorEmitContext>
{
    public GeneratedConstructorEmitContext(string assemblyName, BreadcrumbLevel breadcrumbLevel)
    {
        AssemblyName = assemblyName;
        BreadcrumbLevel = breadcrumbLevel;
    }

    /// <summary>
    /// The current compilation's assembly name, or <c>"Generated"</c> as a fallback.
    /// </summary>
    public string AssemblyName { get; }

    /// <summary>
    /// The configured breadcrumb verbosity, read from the
    /// <c>NeedlrBreadcrumbLevel</c> MSBuild property.
    /// </summary>
    public BreadcrumbLevel BreadcrumbLevel { get; }

    public bool Equals(GeneratedConstructorEmitContext other)
    {
        return string.Equals(AssemblyName, other.AssemblyName, StringComparison.Ordinal) &&
            BreadcrumbLevel == other.BreadcrumbLevel;
    }

    public override bool Equals(object? obj) => obj is GeneratedConstructorEmitContext other && Equals(other);

    public override int GetHashCode()
    {
        unchecked
        {
            return (AssemblyName.GetHashCode() * 397) ^ (int)BreadcrumbLevel;
        }
    }

    public static bool operator ==(GeneratedConstructorEmitContext left, GeneratedConstructorEmitContext right) => left.Equals(right);

    public static bool operator !=(GeneratedConstructorEmitContext left, GeneratedConstructorEmitContext right) => !left.Equals(right);
}
