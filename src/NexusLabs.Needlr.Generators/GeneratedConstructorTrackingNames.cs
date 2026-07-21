namespace NexusLabs.Needlr.Generators;

/// <summary>
/// Defines stable names for generated-constructor incremental pipeline steps.
/// </summary>
internal static class GeneratedConstructorTrackingNames
{
    /// <summary>The raw per-candidate transform.</summary>
    public const string Candidates = "GeneratedConstructor.Candidates";

    /// <summary>The filtered, one-per-type constructor models.</summary>
    public const string Models = "GeneratedConstructor.Models";

    /// <summary>The current compilation's assembly name.</summary>
    public const string AssemblyName = "GeneratedConstructor.AssemblyName";

    /// <summary>The configured breadcrumb verbosity.</summary>
    public const string BreadcrumbLevel = "GeneratedConstructor.BreadcrumbLevel";

    /// <summary>The stable scalar context used during source emission.</summary>
    public const string EmitContext = "GeneratedConstructor.EmitContext";

    /// <summary>The final per-type model and emission-context pairs.</summary>
    public const string Output = "GeneratedConstructor.Output";
}
