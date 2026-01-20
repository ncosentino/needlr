using Microsoft.CodeAnalysis;

namespace NexusLabs.Needlr.Analyzers;

/// <summary>
/// Contains diagnostic descriptors for all Needlr analyzers.
/// </summary>
public static class DiagnosticDescriptors
{
    private const string Category = "NexusLabs.Needlr";
    private const string HelpLinkBase = "https://github.com/nexus-labs/needlr/blob/main/docs/analyzers/";

    /// <summary>
    /// NDLRCOR001: Reflection API used in AOT project.
    /// </summary>
    public static readonly DiagnosticDescriptor ReflectionInAotProject = new(
        id: DiagnosticIds.ReflectionInAotProject,
        title: "Reflection API used in AOT project",
        messageFormat: "'{0}' uses reflection and is not compatible with AOT/trimming. Use source-generated alternatives instead.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Reflection-based Needlr APIs cannot be used in projects with PublishAot or IsAotCompatible enabled. Use source-generated components instead.",
        helpLinkUri: HelpLinkBase + "NDLRCOR001.md");

    /// <summary>
    /// NDLRCOR002: Plugin has constructor dependencies.
    /// </summary>
    public static readonly DiagnosticDescriptor PluginHasConstructorDependencies = new(
        id: DiagnosticIds.PluginHasConstructorDependencies,
        title: "Plugin has constructor dependencies",
        messageFormat: "Plugin '{0}' has constructor parameters but is instantiated before DI is available. Use a parameterless constructor.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "IServiceCollectionPlugin and IPostBuildServiceCollectionPlugin implementations are instantiated before the dependency injection container is built, so they cannot have constructor dependencies.",
        helpLinkUri: HelpLinkBase + "NDLRCOR002.md");
}
