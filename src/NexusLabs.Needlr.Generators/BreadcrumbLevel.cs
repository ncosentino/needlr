namespace NexusLabs.Needlr.Generators;

/// <summary>
/// Controls the level of documentation breadcrumbs included in generated source code.
/// </summary>
internal enum BreadcrumbLevel
{
    /// <summary>
    /// No breadcrumbs beyond the standard auto-generated header.
    /// Use for production builds where file size matters.
    /// </summary>
    None = 0,

    /// <summary>
    /// Brief inline comments explaining what each piece of code does.
    /// This is the default level.
    /// </summary>
    Minimal = 1,

    /// <summary>
    /// Full context boxes with source paths, trigger explanations, and dependency info.
    /// Use for debugging or learning how the generation works.
    /// </summary>
    Verbose = 2
}
