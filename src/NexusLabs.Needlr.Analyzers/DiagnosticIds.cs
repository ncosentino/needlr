namespace NexusLabs.Needlr.Analyzers;

/// <summary>
/// Contains diagnostic IDs for all Needlr analyzers.
/// </summary>
public static class DiagnosticIds
{
    /// <summary>
    /// NDLR0001: Reflection API used in AOT project.
    /// </summary>
    public const string ReflectionInAotProject = "NDLR0001";

    /// <summary>
    /// NDLR0002: Plugin has constructor dependencies.
    /// </summary>
    public const string PluginHasConstructorDependencies = "NDLR0002";
}
