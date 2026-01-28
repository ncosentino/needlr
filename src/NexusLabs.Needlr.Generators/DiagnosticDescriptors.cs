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

    /// <summary>
    /// NDLRGEN006: [OpenDecoratorFor] type argument must be an open generic interface.
    /// </summary>
    public static readonly DiagnosticDescriptor OpenDecoratorTypeNotOpenGeneric = new(
        id: "NDLRGEN006",
        title: "[OpenDecoratorFor] type must be an open generic interface",
        messageFormat: "Type argument '{0}' in [OpenDecoratorFor] is not an open generic interface: {1}",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "The [OpenDecoratorFor] attribute requires an open generic interface type. Use typeof(IInterface<>) syntax, not a closed generic like typeof(IInterface<string>) or a non-generic type.",
        helpLinkUri: HelpLinkBase + "NDLRGEN006.md");

    /// <summary>
    /// NDLRGEN007: [OpenDecoratorFor] decorator class must be an open generic with matching arity.
    /// </summary>
    public static readonly DiagnosticDescriptor OpenDecoratorClassNotOpenGeneric = new(
        id: "NDLRGEN007",
        title: "[OpenDecoratorFor] decorator must be an open generic class",
        messageFormat: "Class '{0}' with [OpenDecoratorFor({1})] must be an open generic class with {2} type parameter(s)",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "When using [OpenDecoratorFor(typeof(IInterface<>))], the decorated class must also be an open generic with the same number of type parameters so it can be closed over each discovered implementation.",
        helpLinkUri: HelpLinkBase + "NDLRGEN007.md");

    /// <summary>
    /// NDLRGEN008: [OpenDecoratorFor] decorator class must implement the open generic interface.
    /// </summary>
    public static readonly DiagnosticDescriptor OpenDecoratorNotImplementingInterface = new(
        id: "NDLRGEN008",
        title: "[OpenDecoratorFor] decorator must implement the interface",
        messageFormat: "Class '{0}' has [OpenDecoratorFor({1})] but does not implement '{1}'. The decorator must implement the interface it decorates.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "A decorator must implement the same interface as the services it wraps. Ensure the class implements the open generic interface specified in [OpenDecoratorFor].",
        helpLinkUri: HelpLinkBase + "NDLRGEN008.md");

    // ============================================================================
    // Options Validation Analyzers (NDLRGEN014-021)
    // ============================================================================

    /// <summary>
    /// NDLRGEN014: Validator type must implement IOptionsValidator&lt;T&gt; or have a valid Validate method.
    /// </summary>
    public static readonly DiagnosticDescriptor ValidatorTypeMissingInterface = new(
        id: "NDLRGEN014",
        title: "Validator type has no validation method",
        messageFormat: "Validator type '{0}' must have a Validate method. Implement IOptionsValidator<{1}> or add a 'Validate({1})' method.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "When using Validator property on [Options], the specified type must have a valid validation method. Either implement IOptionsValidator<T> or add a method matching the expected signature.",
        helpLinkUri: HelpLinkBase + "NDLRGEN014.md");

    /// <summary>
    /// NDLRGEN015: Validator's generic type parameter doesn't match the options type.
    /// </summary>
    public static readonly DiagnosticDescriptor ValidatorTypeMismatch = new(
        id: "NDLRGEN015",
        title: "Validator type mismatch",
        messageFormat: "Validator '{0}' validates '{1}' but is applied to options type '{2}'. The validator must be for the same type.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "The Validator specified in [Options(Validator = ...)] must validate the same type as the options class it's applied to. Check that the IOptionsValidator<T> or validation method parameter matches the options type.",
        helpLinkUri: HelpLinkBase + "NDLRGEN015.md");

    /// <summary>
    /// NDLRGEN016: ValidateMethod specified but method not found on target type.
    /// </summary>
    public static readonly DiagnosticDescriptor ValidateMethodNotFound = new(
        id: "NDLRGEN016",
        title: "Validation method not found",
        messageFormat: "Method '{0}' not found on type '{1}'. Ensure the method exists and has the correct signature.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "The ValidateMethod specified in [Options] must exist on the options type (or Validator type if specified). The method should return IEnumerable<ValidationError> or IEnumerable<string>.",
        helpLinkUri: HelpLinkBase + "NDLRGEN016.md");

    /// <summary>
    /// NDLRGEN017: Validation method has incorrect signature.
    /// </summary>
    public static readonly DiagnosticDescriptor ValidateMethodWrongSignature = new(
        id: "NDLRGEN017",
        title: "Validation method has wrong signature",
        messageFormat: "Method '{0}' on type '{1}' has wrong signature, expected {2}",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Validation methods must return IEnumerable<ValidationError> or IEnumerable<string>. Instance methods on the options type should have no parameters. External validator methods should have one parameter of the options type.",
        helpLinkUri: HelpLinkBase + "NDLRGEN017.md");

    /// <summary>
    /// NDLRGEN018: Validator specified but ValidateOnStart is false.
    /// </summary>
    public static readonly DiagnosticDescriptor ValidatorWontRun = new(
        id: "NDLRGEN018",
        title: "Validator won't run",
        messageFormat: "Validator '{0}' will not run because ValidateOnStart is false. Set ValidateOnStart = true to enable validation.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "When Validator is specified but ValidateOnStart is not enabled, the validator will never be invoked. Either enable ValidateOnStart or remove the Validator property.",
        helpLinkUri: HelpLinkBase + "NDLRGEN018.md");

    /// <summary>
    /// NDLRGEN019: ValidateMethod specified but ValidateOnStart is false.
    /// </summary>
    public static readonly DiagnosticDescriptor ValidateMethodWontRun = new(
        id: "NDLRGEN019",
        title: "Validation method won't run",
        messageFormat: "ValidateMethod '{0}' will not run because ValidateOnStart is false. Set ValidateOnStart = true to enable validation.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "When ValidateMethod is specified but ValidateOnStart is not enabled, the validation method will never be invoked. Either enable ValidateOnStart or remove the ValidateMethod property.",
        helpLinkUri: HelpLinkBase + "NDLRGEN019.md");

    /// <summary>
    /// NDLRGEN021: [OptionsValidator] attribute is deprecated.
    /// </summary>
    public static readonly DiagnosticDescriptor OptionsValidatorDeprecated = new(
        id: "NDLRGEN021",
        title: "[OptionsValidator] is deprecated",
        messageFormat: "[OptionsValidator] on method '{0}' is deprecated. Use convention-based 'Validate()' method or [Options(ValidateMethod = \"...\")] instead.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Info,
        isEnabledByDefault: true,
        description: "The [OptionsValidator] attribute is deprecated in favor of convention-based discovery. Simply name your method 'Validate' (or use ValidateMethod property to specify a custom name). The attribute still works but may be removed in a future version.",
        helpLinkUri: HelpLinkBase + "NDLRGEN021.md");
}
