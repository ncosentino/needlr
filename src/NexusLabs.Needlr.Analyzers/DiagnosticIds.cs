namespace NexusLabs.Needlr.Analyzers;

/// <summary>
/// Contains diagnostic IDs for all Needlr core analyzers.
/// </summary>
/// <remarks>
/// Core analyzer codes use the NDLRCOR prefix.
/// </remarks>
public static class DiagnosticIds
{
    /// <summary>
    /// NDLRCOR001: Reflection API used in AOT project.
    /// </summary>
    public const string ReflectionInAotProject = "NDLRCOR001";

    /// <summary>
    /// NDLRCOR002: Plugin has constructor dependencies.
    /// </summary>
    public const string PluginHasConstructorDependencies = "NDLRCOR002";

    /// <summary>
    /// NDLRCOR003: DeferToContainer attribute in generated code.
    /// </summary>
    public const string DeferToContainerInGeneratedCode = "NDLRCOR003";

    /// <summary>
    /// NDLRCOR004: Injectable type in global namespace may not be discovered.
    /// </summary>
    public const string GlobalNamespaceTypeNotDiscovered = "NDLRCOR004";
}
