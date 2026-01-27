using Microsoft.CodeAnalysis;

namespace NexusLabs.Needlr.Generators;

/// <summary>
/// Contains diagnostic descriptors for the Needlr source generator.
/// </summary>
internal static class DiagnosticDescriptors
{
    private const string Category = "NexusLabs.Needlr.Generators";
    private const string HelpLinkBase = "https://github.com/nexus-labs/needlr/blob/main/docs/analyzers/";

    /// <summary>
    /// NDLRGEN001: Internal type in referenced assembly cannot be registered.
    /// </summary>
    /// <remarks>
    /// This error is emitted when a type in a referenced assembly:
    /// - Matches the namespace filter
    /// - Would be registerable (injectable or plugin) if it were accessible
    /// - Is internal (not public) and thus inaccessible from the generated code
    /// 
    /// To fix this error, add [GenerateTypeRegistry] to the referenced assembly
    /// so that it generates its own type registry that can access its internal types.
    /// </remarks>
    public static readonly DiagnosticDescriptor InaccessibleInternalType = new(
        id: "NDLRGEN001",
        title: "Internal type in referenced assembly cannot be registered",
        messageFormat: "Type '{0}' in assembly '{1}' is internal and cannot be registered. Add [GenerateTypeRegistry] attribute to assembly '{1}' to include its internal types.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Internal types in referenced assemblies cannot be accessed by the generated code. To include internal types from a referenced assembly, that assembly must have its own [GenerateTypeRegistry] attribute so it can generate its own type registry.",
        helpLinkUri: HelpLinkBase + "NDLRGEN001.md");

    /// <summary>
    /// NDLRGEN002: Referenced assembly has internal plugin types but no [GenerateTypeRegistry] attribute.
    /// </summary>
    /// <remarks>
    /// This error is emitted when a referenced assembly:
    /// - Contains internal types that implement plugin interfaces (e.g., IServiceCollectionPlugin)
    /// - Does not have a [GenerateTypeRegistry] attribute
    /// 
    /// Without the attribute, the internal plugin types will not be registered and will
    /// silently fail to load at runtime.
    /// </remarks>
    public static readonly DiagnosticDescriptor MissingGenerateTypeRegistryAttribute = new(
        id: "NDLRGEN002",
        title: "Referenced assembly has internal plugin types but no type registry",
        messageFormat: "Assembly '{0}' contains internal plugin type '{1}' but has no [GenerateTypeRegistry] attribute. Add [GenerateTypeRegistry] to assembly '{0}' to register its internal plugin types, or make the plugin type public.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Referenced assemblies with internal plugin types must have a [GenerateTypeRegistry] attribute to generate their own type registry. Without it, internal plugin types will not be discovered or registered. Alternatively, make the plugin type public so it can be discovered by the host assembly's generator.",
        helpLinkUri: HelpLinkBase + "NDLRGEN002.md");

    /// <summary>
    /// NDLRGEN003: [GenerateFactory] on type with all injectable parameters is unnecessary.
    /// </summary>
    public static readonly DiagnosticDescriptor FactoryAllParamsInjectable = new(
        id: "NDLRGEN003",
        title: "[GenerateFactory] unnecessary - all parameters are injectable",
        messageFormat: "Type '{0}' has [GenerateFactory] but all constructor parameters are injectable. Consider removing the attribute for normal registration.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "The [GenerateFactory] attribute generates a factory for types with mixed injectable and runtime parameters. When all parameters can be injected by the container, a factory adds no value - the type should be registered normally.",
        helpLinkUri: HelpLinkBase + "NDLRGEN003.md");

    /// <summary>
    /// NDLRGEN004: [GenerateFactory] on type with no injectable parameters provides low value.
    /// </summary>
    public static readonly DiagnosticDescriptor FactoryNoInjectableParams = new(
        id: "NDLRGEN004",
        title: "[GenerateFactory] has low value - no injectable parameters",
        messageFormat: "Type '{0}' has [GenerateFactory] but no constructor parameters are injectable. The factory provides little benefit over direct instantiation.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "The [GenerateFactory] attribute is most useful when a type has a mix of injectable and runtime parameters. When no parameters can be injected, the factory is essentially a wrapper around 'new' with no DI benefit.",
        helpLinkUri: HelpLinkBase + "NDLRGEN004.md");

    /// <summary>
    /// NDLRGEN005: [GenerateFactory&lt;T&gt;] type argument must be an interface implemented by the class.
    /// </summary>
    public static readonly DiagnosticDescriptor FactoryTypeArgNotImplemented = new(
        id: "NDLRGEN005",
        title: "[GenerateFactory<T>] type argument not implemented",
        messageFormat: "Type '{0}' has [GenerateFactory<{1}>] but does not implement '{1}'. The type argument must be an interface implemented by the class.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "When using [GenerateFactory<T>], T must be an interface that the decorated class implements. The factory's Create() method and Func<> return T, so the class must be assignable to T.",
        helpLinkUri: HelpLinkBase + "NDLRGEN005.md");
}
