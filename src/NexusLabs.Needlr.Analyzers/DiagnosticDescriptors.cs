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

    /// <summary>
    /// NDLRCOR005: Lifetime mismatch - longer-lived service depends on shorter-lived service.
    /// </summary>
    public static readonly DiagnosticDescriptor LifetimeMismatch = new(
        id: DiagnosticIds.LifetimeMismatch,
        title: "Lifetime mismatch: longer-lived service depends on shorter-lived service",
        messageFormat: "'{0}' is registered as {1} but depends on '{2}' which is registered as {3}. This causes a captive dependency.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "A longer-lived service (e.g., Singleton) depending on a shorter-lived service (e.g., Scoped or Transient) creates a 'captive dependency' where the shorter-lived service is held beyond its intended lifetime. This can cause stale data, memory leaks, or concurrency issues. Consider changing the lifetimes or using a factory pattern.",
        helpLinkUri: HelpLinkBase + "NDLRCOR005.md");

    /// <summary>
    /// NDLRCOR006: Circular dependency detected in service registration.
    /// </summary>
    public static readonly DiagnosticDescriptor CircularDependency = new(
        id: DiagnosticIds.CircularDependency,
        title: "Circular dependency detected",
        messageFormat: "Circular dependency detected: {0}. This will cause a runtime exception when resolving the service.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "A circular dependency occurs when a service directly or indirectly depends on itself. This will cause a StackOverflowException or InvalidOperationException at runtime. Break the cycle by introducing a factory, lazy injection, or redesigning the dependencies.",
        helpLinkUri: HelpLinkBase + "NDLRCOR006.md",
        customTags: WellKnownDiagnosticTags.CompilationEnd);
}
