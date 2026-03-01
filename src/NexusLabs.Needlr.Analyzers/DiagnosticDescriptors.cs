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

    /// <summary>
    /// NDLRCOR007: Intercept attribute type must implement IMethodInterceptor.
    /// </summary>
    public static readonly DiagnosticDescriptor InterceptTypeMustImplementInterface = new(
        id: DiagnosticIds.InterceptTypeMustImplementInterface,
        title: "Intercept type must implement IMethodInterceptor",
        messageFormat: "Type '{0}' used in [Intercept] attribute does not implement IMethodInterceptor",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "The type specified in an [Intercept] or [Intercept<T>] attribute must implement IMethodInterceptor. Interceptors must implement this interface to intercept method calls.",
        helpLinkUri: HelpLinkBase + "NDLRCOR007.md");

    /// <summary>
    /// NDLRCOR008: [Intercept] applied to class without interfaces.
    /// </summary>
    public static readonly DiagnosticDescriptor InterceptOnClassWithoutInterfaces = new(
        id: DiagnosticIds.InterceptOnClassWithoutInterfaces,
        title: "[Intercept] applied to class without interfaces",
        messageFormat: "Class '{0}' has [Intercept] attribute but does not implement any interfaces. Interceptors require interface-based resolution.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Interceptors work by generating a proxy class that implements the service's interfaces. If the class doesn't implement any interfaces, the interceptor cannot be applied. Add an interface to the class or remove the [Intercept] attribute.",
        helpLinkUri: HelpLinkBase + "NDLRCOR008.md");

    /// <summary>
    /// NDLRCOR009: Lazy&lt;T&gt; references type not discovered by source generation.
    /// </summary>
    public static readonly DiagnosticDescriptor LazyResolutionUnknown = new(
        id: DiagnosticIds.LazyResolutionUnknown,
        title: "Lazy<T> references undiscovered type",
        messageFormat: "Type '{0}' in Lazy<{0}> was not discovered by source generation. If registered via reflection, this is expected.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Info,
        isEnabledByDefault: true,
        description: "The type parameter in Lazy<T> was not found in the source-generated type registry. This may indicate a missing registration, or the type may be registered via reflection at runtime. Use .editorconfig to suppress if intentional.",
        helpLinkUri: HelpLinkBase + "NDLRCOR009.md",
        customTags: WellKnownDiagnosticTags.CompilationEnd);

    /// <summary>
    /// NDLRCOR010: IEnumerable&lt;T&gt; has no implementations discovered.
    /// </summary>
    public static readonly DiagnosticDescriptor CollectionResolutionEmpty = new(
        id: DiagnosticIds.CollectionResolutionEmpty,
        title: "IEnumerable<T> has no discovered implementations",
        messageFormat: "No implementations of '{0}' were discovered by source generation. The collection will be empty unless registered via reflection.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Info,
        isEnabledByDefault: true,
        description: "No types implementing the interface were found in the source-generated type registry. This may indicate missing registrations, or implementations may be registered via reflection at runtime. Use .editorconfig to suppress if intentional.",
        helpLinkUri: HelpLinkBase + "NDLRCOR010.md",
        customTags: WellKnownDiagnosticTags.CompilationEnd);

    /// <summary>
    /// NDLRCOR011: [FromKeyedServices] references a key with no known registration.
    /// </summary>
    public static readonly DiagnosticDescriptor KeyedServiceUnknownKey = new(
        id: DiagnosticIds.KeyedServiceUnknownKey,
        title: "[FromKeyedServices] references unknown key",
        messageFormat: "Keyed service '{0}' with key \"{1}\" has no known registration. If registered via plugin or reflection, this is expected.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Info,
        isEnabledByDefault: true,
        description: "The keyed service reference was not found in source-generated registrations. Keyed services are typically registered via IServiceCollectionPlugin, which the analyzer cannot validate. Use .editorconfig to suppress if intentional.",
        helpLinkUri: HelpLinkBase + "NDLRCOR011.md",
        customTags: WellKnownDiagnosticTags.CompilationEnd);

    /// <summary>
    /// NDLRCOR015: [RegisterAs&lt;T&gt;] type argument must be an interface implemented by the class.
    /// </summary>
    public static readonly DiagnosticDescriptor RegisterAsTypeArgNotImplemented = new(
        id: DiagnosticIds.RegisterAsTypeArgNotImplemented,
        title: "[RegisterAs<T>] type argument not implemented",
        messageFormat: "Type '{0}' has [RegisterAs<{1}>] but does not implement '{1}'. The type argument must be an interface implemented by the class.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "When using [RegisterAs<T>], T must be an interface that the decorated class implements. The class will only be registered as the specified interface, so it must actually implement it.",
        helpLinkUri: HelpLinkBase + "NDLRCOR015.md");

    /// <summary>
    /// NDLRCOR012: Disposable captive dependency - longer-lived service holds IDisposable with shorter lifetime.
    /// </summary>
    public static readonly DiagnosticDescriptor DisposableCaptiveDependency = new(
        id: DiagnosticIds.DisposableCaptiveDependency,
        title: "Disposable captive dependency",
        messageFormat: "'{0}' ({1}) depends on '{2}' ({3}) which implements {4}. The disposable will be disposed when its scope ends while '{0}' continues to hold a reference.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "A longer-lived service holds a reference to a shorter-lived IDisposable/IAsyncDisposable. When the shorter-lived scope ends, the dependency will be disposed while the longer-lived service still holds a reference, causing ObjectDisposedException at runtime. Use a factory pattern (Func<T>) or IServiceScopeFactory to resolve scoped dependencies on demand.",
        helpLinkUri: HelpLinkBase + "NDLRCOR012.md");

    /// <summary>
    /// NDLRCOR016: [DoNotAutoRegister] applied directly to a plugin class is redundant.
    /// </summary>
    public static readonly DiagnosticDescriptor DoNotAutoRegisterOnPluginClass = new(
        id: DiagnosticIds.DoNotAutoRegisterOnPluginClass,
        title: "[DoNotAutoRegister] on plugin class is redundant",
        messageFormat: "'{0}' has [DoNotAutoRegister] directly applied. The plugin interface it implements already carries this attribute, so applying it to the implementing class is not needed and was previously incorrectly suppressing plugin discovery.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Plugin interfaces such as IServiceCollectionPlugin already carry [DoNotAutoRegister] to prevent the interface itself from being registered as a DI service. Adding [DoNotAutoRegister] directly to an implementing class is redundant and was incorrectly suppressing plugin discovery in earlier versions. Remove the attribute from the class.",
        helpLinkUri: HelpLinkBase + "NDLRCOR016.md");
}
