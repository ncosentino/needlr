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

    /// <summary>
    /// NDLRCOR003: DeferToContainer attribute in generated code.
    /// </summary>
    public static readonly DiagnosticDescriptor DeferToContainerInGeneratedCode = new(
        id: DiagnosticIds.DeferToContainerInGeneratedCode,
        title: "[DeferToContainer] attribute in generated code is ignored",
        messageFormat: "[DeferToContainer] on '{0}' is in generated code and will be ignored by Needlr. Move the attribute to your original partial class declaration.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Source generators run in isolation and cannot see attributes added by other generators. The [DeferToContainer] attribute must be placed on the original user-written partial class declaration, not in generated code.",
        helpLinkUri: HelpLinkBase + "NDLRCOR003.md");

    /// <summary>
    /// NDLRCOR004: Injectable type in global namespace may not be discovered.
    /// </summary>
    public static readonly DiagnosticDescriptor GlobalNamespaceTypeNotDiscovered = new(
        id: DiagnosticIds.GlobalNamespaceTypeNotDiscovered,
        title: "Injectable type in global namespace may not be discovered",
        messageFormat: "Type '{0}' is in the global namespace and won't be discovered when IncludeNamespacePrefixes is set. Add a namespace or include \"\" in IncludeNamespacePrefixes.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Types in the global namespace are not matched by namespace prefix filters. Either move the type to a namespace that matches your IncludeNamespacePrefixes, or add an empty string (\"\") to IncludeNamespacePrefixes to include global namespace types.",
        helpLinkUri: HelpLinkBase + "NDLRCOR004.md");
}
