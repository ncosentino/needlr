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

    // ============================================================================
    // AOT Compatibility Analyzers (NDLRGEN020+)
    // ============================================================================

    /// <summary>
    /// NDLRGEN020: [Options] attribute is not compatible with AOT/trimming.
    /// </summary>
    /// <remarks>
    /// The [Options] feature generates calls to Configure&lt;T&gt;() and BindConfiguration()
    /// which use reflection for configuration binding. These APIs are not AOT-compatible.
    /// </remarks>
    public static readonly DiagnosticDescriptor OptionsNotAotCompatible = new(
        id: "NDLRGEN020",
        title: "[Options] is not compatible with AOT",
        messageFormat: "Type '{0}' has [Options] attribute but is in an AOT-enabled project. The [Options] feature uses configuration binding APIs that are not compatible with Native AOT or trimming.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "The [Options] attribute generates code that calls Configure<T>() and BindConfiguration() which use reflection for property binding. These APIs have [RequiresDynamicCode] and [RequiresUnreferencedCode] attributes, making them incompatible with Native AOT and trimming. Remove the [Options] attribute or disable AOT/trimming for this project.",
        helpLinkUri: HelpLinkBase + "NDLRGEN020.md");

    /// <summary>
    /// NDLRGEN021: Positional record with [Options] must be declared partial.
    /// </summary>
    /// <remarks>
    /// Positional records lack parameterless constructors, which are required for
    /// configuration binding. When the record is partial, the generator can emit
    /// a parameterless constructor. Non-partial records cannot be extended.
    /// </remarks>
    public static readonly DiagnosticDescriptor PositionalRecordMustBePartial = new(
        id: "NDLRGEN021",
        title: "Positional record must be partial for [Options]",
        messageFormat: "Positional record '{0}' has [Options] but is not declared partial. Add the 'partial' modifier to enable configuration binding, or use a record with init-only properties instead.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Positional records (records with primary constructor parameters) lack parameterless constructors, which Microsoft's configuration binder requires. When the record is declared partial, the generator emits a parameterless constructor that chains to the primary constructor. Without partial, the record cannot work with configuration binding at runtime.",
        helpLinkUri: HelpLinkBase + "NDLRGEN021.md");

    /// <summary>
    /// NDLRGEN022: Disposable service may be captured by a longer-lived service.
    /// </summary>
    /// <remarks>
    /// This error is emitted when a longer-lived service (e.g., Singleton) has a constructor
    /// dependency on a shorter-lived service (e.g., Scoped, Transient) that implements
    /// IDisposable or IAsyncDisposable. This is a "captive dependency" anti-pattern where
    /// the disposable may be disposed while the consuming service still holds a reference.
    ///
    /// Unlike NDLRCOR012 which only detects explicit lifetime attributes, this diagnostic
    /// uses inferred lifetimes from Needlr's convention-based discovery, catching more issues.
    /// </remarks>
    public static readonly DiagnosticDescriptor DisposableCaptiveDependency = new(
        id: "NDLRGEN022",
        title: "Disposable captive dependency detected",
        messageFormat: "'{0}' ({1}) depends on '{2}' ({3}) which implements IDisposable. The disposable may be disposed while '{0}' still holds a reference to it.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "A longer-lived service captures a shorter-lived disposable dependency. When the shorter-lived scope ends, the dependency will be disposed while the consuming service continues to use it. This can cause ObjectDisposedException at runtime. Use Func<T> or IServiceScopeFactory to create new instances on demand.",
        helpLinkUri: HelpLinkBase + "NDLRGEN022.md");

    /// <summary>
    /// NDLRGEN030: DataAnnotation attribute cannot be source-generated.
    /// </summary>
    public static readonly DiagnosticDescriptor UnsupportedDataAnnotation = new(
        id: "NDLRGEN030",
        title: "DataAnnotation attribute cannot be source-generated",
        messageFormat: "DataAnnotation '{0}' on '{1}.{2}' cannot be source-generated. In AOT mode, this validation will not run. Consider using a custom Validate() method instead.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "This DataAnnotation validation attribute cannot be source-generated because it requires runtime reflection or invokes arbitrary code. In AOT mode, validation will silently skip this attribute. In non-AOT mode, the reflection-based .ValidateDataAnnotations() fallback will handle it. Consider adding a custom Validate() method that performs equivalent validation.",
        helpLinkUri: HelpLinkBase + "NDLRGEN030.md");

    // ============================================================================
    // Provider Analyzers (NDLRGEN031-034)
    // ============================================================================

    /// <summary>
    /// NDLRGEN031: [Provider] on class requires `partial` modifier.
    /// </summary>
    public static readonly DiagnosticDescriptor ProviderClassNotPartial = new(
        id: "NDLRGEN031",
        title: "[Provider] on class requires partial modifier",
        messageFormat: "Class '{0}' has [Provider] attribute but is not declared partial. Add the 'partial' modifier to enable provider generation.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "When applying [Provider] to a class (shorthand mode), the class must be declared partial so the generator can add an interface implementation and constructor. Without the partial modifier, the generator cannot extend the class.",
        helpLinkUri: HelpLinkBase + "NDLRGEN031.md");

    /// <summary>
    /// NDLRGEN032: [Provider] interface must only contain get-only properties.
    /// </summary>
    public static readonly DiagnosticDescriptor ProviderInterfaceInvalidMember = new(
        id: "NDLRGEN032",
        title: "[Provider] interface has invalid member",
        messageFormat: "Interface '{0}' has [Provider] attribute but contains {1}. Provider interfaces must only contain get-only properties.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Provider interfaces should only contain get-only properties that represent the services to be provided. Methods, events, indexers, and settable properties are not supported. Consider using a class with [Provider(typeof(T), ...)] shorthand syntax instead.",
        helpLinkUri: HelpLinkBase + "NDLRGEN032.md");

    /// <summary>
    /// NDLRGEN033: Provider property type is a concrete class, consider using an interface.
    /// </summary>
    public static readonly DiagnosticDescriptor ProviderPropertyConcreteType = new(
        id: "NDLRGEN033",
        title: "Provider property uses concrete type",
        messageFormat: "Provider property '{0}.{1}' has type '{2}' which is a concrete class. Consider using an interface for better testability and flexibility.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Provider properties represent services resolved from the DI container. Using interface types instead of concrete classes improves testability, allows for easier mocking, and follows dependency inversion principles. This is a recommendation, not a requirement.",
        helpLinkUri: HelpLinkBase + "NDLRGEN033.md");

    /// <summary>
    /// NDLRGEN034: Circular provider dependency detected.
    /// </summary>
    public static readonly DiagnosticDescriptor ProviderCircularDependency = new(
        id: "NDLRGEN034",
        title: "Circular provider dependency detected",
        messageFormat: "Provider '{0}' has a circular dependency: {1}",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "A circular dependency was detected in the provider dependency graph. Provider A references Provider B which references Provider A (directly or indirectly). This will cause a stack overflow at runtime. Break the cycle by removing one of the provider references or restructuring the dependencies.",
        helpLinkUri: HelpLinkBase + "NDLRGEN034.md");

    // ============================================================================
    // Composition Analyzers (NDLRGEN035-038)
    // ============================================================================

    /// <summary>
    /// NDLRGEN035: [RegisterClosedOverImplementationsOf] source type must be an open generic interface.
    /// </summary>
    public static readonly DiagnosticDescriptor ComposedSourceNotOpenGenericInterface = new(
        id: "NDLRGEN035",
        title: "[RegisterClosedOverImplementationsOf] source type must be an open generic interface",
        messageFormat: "Source type argument '{0}' in [RegisterClosedOverImplementationsOf] is not an open generic interface: {1}",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "The [RegisterClosedOverImplementationsOf] attribute requires an open generic interface type whose concrete closed implementations drive registration. Use typeof(IInterface<>) syntax, not a closed generic like typeof(IInterface<string>) or a non-generic type.",
        helpLinkUri: HelpLinkBase + "NDLRGEN035.md");

    /// <summary>
    /// NDLRGEN036: [RegisterClosedOverImplementationsOf] composition class must be an open generic with matching arity.
    /// </summary>
    public static readonly DiagnosticDescriptor ComposedClassNotOpenGeneric = new(
        id: "NDLRGEN036",
        title: "[RegisterClosedOverImplementationsOf] composition must be an open generic class",
        messageFormat: "Class '{0}' with [RegisterClosedOverImplementationsOf({1})] must be an open generic class with {2} type parameter(s)",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "When using [RegisterClosedOverImplementationsOf(typeof(IInterface<>))], the annotated composition class must be an open generic with the same number of type parameters so it can be closed over each discovered implementation's type argument(s).",
        helpLinkUri: HelpLinkBase + "NDLRGEN036.md");

    /// <summary>
    /// NDLRGEN037: [RegisterClosedOverImplementationsOf] composition class must implement the As service type.
    /// </summary>
    public static readonly DiagnosticDescriptor ComposedClassNotImplementingAs = new(
        id: "NDLRGEN037",
        title: "[RegisterClosedOverImplementationsOf] composition must implement the As service type",
        messageFormat: "Class '{0}' must implement the 'As' service type specified by [RegisterClosedOverImplementationsOf]",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Each closed composition is registered as the service type named by 'As', so the annotated class must specify an 'As' type and implement it (directly or transitively). Set As = typeof(IFacade) where the composition implements IFacade.",
        helpLinkUri: HelpLinkBase + "NDLRGEN037.md");

    /// <summary>
    /// NDLRGEN038: A discovered type argument cannot legally close the composition type.
    /// </summary>
    /// <remarks>
    /// Emitted by the generator (not the per-attribute analyzer) because only the generator knows the
    /// full set of discovered implementations. The offending registration is skipped rather than emitting
    /// code that fails to compile, turning a would-be runtime absence into a build-time signal.
    /// </remarks>
    public static readonly DiagnosticDescriptor ComposedTypeArgumentViolatesConstraints = new(
        id: "NDLRGEN038",
        title: "[RegisterClosedOverImplementationsOf] discovered type argument violates composition constraints",
        messageFormat: "Composition '{0}' cannot be closed over type argument(s) '{1}' from an implementation of '{2}' because they violate its generic constraints; the registration was skipped",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "An implementation of the designated open generic interface was discovered whose type argument(s) do not satisfy the composition type's generic constraints. Relax the composition's constraints, constrain the source interface so such implementations cannot exist, or exclude the implementation. The registration is skipped to avoid emitting code that would fail to compile.",
        helpLinkUri: HelpLinkBase + "NDLRGEN038.md");

    // ============================================================================
    // Generated Constructor Analyzer (NDLRGEN039-056)
    // ============================================================================

    /// <summary>
    /// NDLRGEN039: A type using generated-constructor generation must be declared partial.
    /// </summary>
    public static readonly DiagnosticDescriptor GeneratedConstructorRequiresPartialType = new(
        id: "NDLRGEN039",
        title: "Generated-constructor type must be partial",
        messageFormat: "Type '{0}' must be declared 'partial' because it uses [GenerateConstructor] or a field-level constructor guard trigger, and a source generator can only add a constructor to a partial type",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Roslyn source generators can only contribute new members to a type declared partial. Add the 'partial' modifier to the class so the generated constructor can be added.",
        helpLinkUri: HelpLinkBase + "NDLRGEN039.md");

    /// <summary>
    /// NDLRGEN040: A record type or nested type cannot use generated-constructor generation.
    /// </summary>
    public static readonly DiagnosticDescriptor GeneratedConstructorUnsupportedTypeShape = new(
        id: "NDLRGEN040",
        title: "Generated-constructor type shape is unsupported",
        messageFormat: "Type '{0}' cannot use generated-constructor generation because it is {1}",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Generated-constructor generation only supports top-level, non-record partial classes. Records and nested types are unsupported and must not carry [GenerateConstructor] or a field-level constructor guard trigger.",
        helpLinkUri: HelpLinkBase + "NDLRGEN040.md");

    /// <summary>
    /// NDLRGEN041: An explicit instance constructor conflicts with generated-constructor generation.
    /// </summary>
    public static readonly DiagnosticDescriptor GeneratedConstructorConflictsWithExplicitConstructor = new(
        id: "NDLRGEN041",
        title: "Generated-constructor conflicts with an explicit constructor",
        messageFormat: "Type '{0}' declares an explicit instance constructor, which conflicts with generated-constructor generation; remove the explicit constructor or remove [GenerateConstructor] and every field-level constructor guard trigger",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Generated-constructor generation requires exactly one unambiguous constructor. A type with its own explicit instance constructor is skipped entirely rather than generating a second, conflicting constructor.",
        helpLinkUri: HelpLinkBase + "NDLRGEN041.md");

    /// <summary>
    /// NDLRGEN042: The base type must expose an accessible parameterless constructor.
    /// </summary>
    public static readonly DiagnosticDescriptor GeneratedConstructorBaseTypeRequiresParameterlessConstructor = new(
        id: "NDLRGEN042",
        title: "Generated-constructor base type requires a parameterless constructor",
        messageFormat: "Type '{0}' derives from '{1}', which has no accessible parameterless constructor; generated-constructor generation requires the implicit 'base()' call to succeed",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "The generated constructor relies on the implicit base() call. A base type that requires constructor arguments is unsupported; add an accessible parameterless constructor to the base type or remove the generated-constructor trigger.",
        helpLinkUri: HelpLinkBase + "NDLRGEN042.md");

    /// <summary>
    /// NDLRGEN043: No eligible field exists for generated-constructor generation.
    /// </summary>
    public static readonly DiagnosticDescriptor GeneratedConstructorNoEligibleFields = new(
        id: "NDLRGEN043",
        title: "No eligible field for generated-constructor generation",
        messageFormat: "Type '{0}' has no private readonly instance field without an initializer, so generated-constructor generation has nothing to generate",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Generated-constructor generation requires at least one eligible field: a private, instance, readonly field without an initializer that is not excluded by [ConstructorIgnore]. Add an eligible field or remove the generation trigger.",
        helpLinkUri: HelpLinkBase + "NDLRGEN043.md");

    /// <summary>
    /// NDLRGEN044: Normalized constructor parameter names collide.
    /// </summary>
    public static readonly DiagnosticDescriptor GeneratedConstructorParameterNameCollision = new(
        id: "NDLRGEN044",
        title: "Generated-constructor parameter names collide",
        messageFormat: "Type '{0}' has two or more eligible fields that normalize to the same constructor parameter name '{1}'; rename one of the conflicting fields",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Field names are normalized to constructor parameter names by removing a leading underscore and lower-casing the first letter. Two fields that normalize to the same name make the generated constructor's parameter list ambiguous.",
        helpLinkUri: HelpLinkBase + "NDLRGEN044.md");

    /// <summary>
    /// NDLRGEN045: A constructor guard exclusion attribute has no effect.
    /// </summary>
    public static readonly DiagnosticDescriptor ConstructorGuardAttributeHasNoEffect = new(
        id: "NDLRGEN045",
        title: "Constructor guard attribute has no effect",
        messageFormat: "Field '{0}' has {1}, but its containing type has no [GenerateConstructor] and no other field has a positive constructor guard trigger, so no constructor is generated and this attribute has no effect",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "[ConstructorIgnore] and [ConstructorGuard(ConstructorGuardKind.None)] are exclusion-only modifiers. Applying one without [GenerateConstructor] on the class or a positive guard trigger elsewhere in the class produces no generated constructor, so the attribute has no effect. Remove the attribute, or add a generation trigger.",
        helpLinkUri: HelpLinkBase + "NDLRGEN045.md");

    /// <summary>
    /// NDLRGEN046: A constructor guard attribute is applied to an ineligible field.
    /// </summary>
    public static readonly DiagnosticDescriptor ConstructorGuardAttributeOnIneligibleField = new(
        id: "NDLRGEN046",
        title: "Constructor guard attribute applied to an ineligible field",
        messageFormat: "Field '{0}' cannot participate in generated-constructor generation because it is {1}, so its constructor guard attribute has no effect",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Only a private, instance, readonly field without an initializer can become a generated-constructor parameter. Remove the constructor guard attribute, or change the field's declaration so it is eligible.",
        helpLinkUri: HelpLinkBase + "NDLRGEN046.md");

    /// <summary>
    /// NDLRGEN047: A guard-mode enum argument is not a defined value.
    /// </summary>
    public static readonly DiagnosticDescriptor InvalidConstructorGuardEnumValue = new(
        id: "NDLRGEN047",
        title: "Invalid constructor guard enum value",
        messageFormat: "'{0}' is not a defined {1} value",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "ConstructorGuardKind and ConstructorNullGuardMode arguments must be one of the enum's defined members. A value produced by casting an undefined integer is rejected at compile time instead of silently falling back to a default.",
        helpLinkUri: HelpLinkBase + "NDLRGEN047.md");

    /// <summary>
    /// NDLRGEN048: A built-in guard kind is incompatible with the field's type.
    /// </summary>
    public static readonly DiagnosticDescriptor ConstructorGuardIncompatibleWithFieldType = new(
        id: "NDLRGEN048",
        title: "Constructor guard incompatible with member type",
        messageFormat: "The '{0}' guard cannot be used on member '{1}' of type '{2}': {3}",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "NotNullOrEmpty and NotNullOrWhiteSpace only apply to string-compatible fields or properties. NotNull only applies where a runtime null is actually possible: reference types (nullable or not) and Nullable<T> value types. Remove the guard, or choose a compatible guard kind.",
        helpLinkUri: HelpLinkBase + "NDLRGEN048.md");

    /// <summary>
    /// NDLRGEN049: A custom guard type is missing or inaccessible.
    /// </summary>
    public static readonly DiagnosticDescriptor ConstructorGuardTypeInvalid = new(
        id: "NDLRGEN049",
        title: "Custom constructor guard type is invalid",
        messageFormat: "The custom guard type for member '{0}' is invalid: {1}",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "A custom constructor guard type must exist and be accessible from the generated constructor. Fix the referenced type, or its accessibility.",
        helpLinkUri: HelpLinkBase + "NDLRGEN049.md");

    /// <summary>
    /// NDLRGEN050: A custom guard method name is empty or whitespace.
    /// </summary>
    public static readonly DiagnosticDescriptor ConstructorGuardMethodNameInvalid = new(
        id: "NDLRGEN050",
        title: "Custom constructor guard method name is invalid",
        messageFormat: "The custom guard method name for member '{0}' must not be empty or consist only of white space",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "An explicit custom guard method name must be a non-empty, non-whitespace identifier, typically supplied through nameof().",
        helpLinkUri: HelpLinkBase + "NDLRGEN050.md");

    /// <summary>
    /// NDLRGEN051: No compatible custom guard method was found.
    /// </summary>
    public static readonly DiagnosticDescriptor ConstructorGuardMethodInvalid = new(
        id: "NDLRGEN051",
        title: "Custom constructor guard method is invalid",
        messageFormat: "Guard method '{0}' on type '{1}' is not valid for member '{2}' of type '{3}': {4}",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "A custom guard method must be an accessible static method returning void with exactly two parameters: a value compatible with the guarded member's type, and a string parameter name. The generator calls this method directly, so its shape must be resolvable at compile time.",
        helpLinkUri: HelpLinkBase + "NDLRGEN051.md");

    /// <summary>
    /// NDLRGEN052: Multiple custom guard method overloads match the field.
    /// </summary>
    public static readonly DiagnosticDescriptor ConstructorGuardMethodAmbiguous = new(
        id: "NDLRGEN052",
        title: "Custom constructor guard method is ambiguous",
        messageFormat: "Multiple accessible static methods named '{0}' on type '{1}' are compatible with member '{2}' of type '{3}'; the generator requires exactly one unambiguous match",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Overload resolution for a custom guard method is intentionally simple and direct rather than full C# overload resolution. Remove or rename the extra overload(s) so exactly one method matches the guarded member's type.",
        helpLinkUri: HelpLinkBase + "NDLRGEN052.md");

    /// <summary>
    /// NDLRGEN053: [ConstructorGuardDefinition] is applied to an unsupported attribute type.
    /// </summary>
    public static readonly DiagnosticDescriptor ConstructorGuardDefinitionTargetInvalid = new(
        id: "NDLRGEN053",
        title: "[ConstructorGuardDefinition] target is invalid",
        messageFormat: "'{0}' cannot carry [ConstructorGuardDefinition] because it is {1}",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "[ConstructorGuardDefinition] must decorate an Attribute-derived type whose [AttributeUsage] allows AttributeTargets.Field or AttributeTargets.Property, so the alias can participate in at least one supported generated-constructor feature.",
        helpLinkUri: HelpLinkBase + "NDLRGEN053.md");

    /// <summary>
    /// NDLRGEN054: A [ConstructorGuardDefinition]'s own guard reference is unresolved.
    /// </summary>
    public static readonly DiagnosticDescriptor ConstructorGuardDefinitionUnresolvedGuard = new(
        id: "NDLRGEN054",
        title: "[ConstructorGuardDefinition] guard contract is unresolved",
        messageFormat: "[ConstructorGuardDefinition] on '{0}' is invalid: {1}",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "The guard type and method referenced by [ConstructorGuardDefinition] must resolve to the same accessible static Validate-compatible method contract required by a direct [ConstructorGuard] custom guard. Fix the referenced type or method on the alias attribute's definition.",
        helpLinkUri: HelpLinkBase + "NDLRGEN054.md");

    /// <summary>
    /// NDLRGEN055: A parameterized custom guard alias usage forwards an argument shape
    /// or a named argument/property that this version does not support forwarding.
    /// </summary>
    public static readonly DiagnosticDescriptor ConstructorGuardAliasUsageArgumentUnsupported = new(
        id: "NDLRGEN055",
        title: "Constructor guard alias usage argument is unsupported",
        messageFormat: "The alias attribute usage on member '{0}' is invalid: {1}",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "A parameterized custom guard alias forwards every positional constructor argument of its usage onto the resolved guard method call, in declared order. Only null, bool, integral, char, string, enum, and Type argument shapes can be forwarded; arrays/params and float/double are not. Named attribute arguments and properties are never forwarded. Change the alias usage to avoid the unsupported argument, or forward it as a supported positional argument instead.",
        helpLinkUri: HelpLinkBase + "NDLRGEN055.md");

    /// <summary>
    /// NDLRGEN056: A custom guard method is incompatible with the arguments a
    /// parameterized alias usage forwards to it, either because the method's effective
    /// arity does not match the number of forwarded arguments, or because one of the
    /// forwarded argument types is incompatible with the corresponding parameter.
    /// </summary>
    public static readonly DiagnosticDescriptor ConstructorGuardForwardedArgumentIncompatible = new(
        id: "NDLRGEN056",
        title: "Custom constructor guard method is incompatible with forwarded alias arguments",
        messageFormat: "Guard method '{0}' on type '{1}' cannot accept the arguments forwarded for member '{2}' of type '{3}': {4}",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "A parameterized custom guard alias usage forwards its own positional constructor arguments between the guarded value and the trailing string parameter name. The resolved guard method must declare exactly one middle parameter per forwarded argument, and each middle parameter's type must be compatible with its corresponding forwarded argument's type (directly, or via generic method type-parameter unification with the guarded member's type). Change the number or types of the alias usage's arguments, or the guard method's middle parameters, so they match exactly.",
        helpLinkUri: HelpLinkBase + "NDLRGEN056.md");

    // ============================================================================
    // Record Constructor Overload Analyzer (NDLRGEN057-062)
    // ============================================================================

    /// <summary>
    /// NDLRGEN057: A record receiving a generated constructor overload must be partial.
    /// </summary>
    public static readonly DiagnosticDescriptor RecordConstructorOverloadRequiresPartialType = new(
        id: "NDLRGEN057",
        title: "Record constructor-overload type must be partial",
        messageFormat: "Record '{0}' must be declared 'partial' because [RecordConstructorOverloadParameter] requires a source generator to add a constructor",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "A generated forwarding constructor can only be contributed to a partial positional record class. Add the partial modifier to the record declaration.",
        helpLinkUri: HelpLinkBase + "NDLRGEN057.md");

    /// <summary>
    /// NDLRGEN058: The marked property's containing type is not a supported record.
    /// </summary>
    public static readonly DiagnosticDescriptor RecordConstructorOverloadUnsupportedTypeShape = new(
        id: "NDLRGEN058",
        title: "Record constructor-overload type shape is unsupported",
        messageFormat: "Type '{0}' cannot use [RecordConstructorOverloadParameter] because it is {1}",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Record constructor-overload generation supports only top-level, non-file-local positional record classes with no record base type. Ordinary classes, record structs, body-only records, file-local records, nested records, and inherited records are unsupported.",
        helpLinkUri: HelpLinkBase + "NDLRGEN058.md");

    /// <summary>
    /// NDLRGEN059: A marked property cannot be assigned by the generated overload.
    /// </summary>
    public static readonly DiagnosticDescriptor RecordConstructorOverloadPropertyUnsupported = new(
        id: "NDLRGEN059",
        title: "Record constructor-overload property is unsupported",
        messageFormat: "Property '{0}' cannot participate in a generated record constructor overload because it is {1}",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "A participating property must be a non-static, non-indexer, non-required, assignable instance property declared directly by the positional record, must not be represented by the primary parameter list, and must use a type accessible from the generated public constructor.",
        helpLinkUri: HelpLinkBase + "NDLRGEN059.md");

    /// <summary>
    /// NDLRGEN060: A property guard has no participating record-overload parameter.
    /// </summary>
    public static readonly DiagnosticDescriptor ConstructorGuardOnNonparticipatingProperty = new(
        id: "NDLRGEN060",
        title: "Constructor guard property does not participate in an overload",
        messageFormat: "Property '{0}' has a constructor guard but is not marked with [RecordConstructorOverloadParameter], so no generated record constructor parameter can invoke the guard",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Constructor guards on properties are valid only for properties marked with [RecordConstructorOverloadParameter]. Add the marker or remove the guard.",
        helpLinkUri: HelpLinkBase + "NDLRGEN060.md");

    /// <summary>
    /// NDLRGEN061: Field-based constructor generation conflicts with the record-only feature.
    /// </summary>
    public static readonly DiagnosticDescriptor RecordConstructorOverloadConflictsWithGeneratedConstructor = new(
        id: "NDLRGEN061",
        title: "Record constructor overload conflicts with field-based generation",
        messageFormat: "Type '{0}' cannot combine [RecordConstructorOverloadParameter] with [GenerateConstructor] or a field-level generated-constructor trigger; use only one constructor-generation model",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "The field-based generated-constructor feature owns an ordinary class construction path and participates in Needlr DI metadata, while the record feature adds a forwarding overload only for positional records and intentionally does not participate in DI. The two contracts cannot be combined on the same type.",
        helpLinkUri: HelpLinkBase + "NDLRGEN061.md");

    /// <summary>
    /// NDLRGEN062: The proposed constructor signature already exists.
    /// </summary>
    public static readonly DiagnosticDescriptor RecordConstructorOverloadSignatureCollision = new(
        id: "NDLRGEN062",
        title: "Record constructor-overload signature collides",
        messageFormat: "Record '{0}' cannot receive the generated constructor overload because its C# signature would collide with existing constructor '{1}'",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Constructor signature identity ignores parameter names, nullable annotations, optional values, and params. Change a marked property's type or remove the conflicting constructor so the generated overload has a unique signature.",
        helpLinkUri: HelpLinkBase + "NDLRGEN062.md");

    // ============================================================================
    // HttpClient Analyzers (NDLRHTTP001-006)
    // ============================================================================

    /// <summary>
    /// NDLRHTTP001: [HttpClientOptions] target must implement INamedHttpClientOptions.
    /// </summary>
    public static readonly DiagnosticDescriptor HttpClientMustImplementMarker = new(
        id: "NDLRHTTP001",
        title: "[HttpClientOptions] target must implement INamedHttpClientOptions",
        messageFormat: "Type '{0}' has [HttpClientOptions] but does not implement INamedHttpClientOptions. Implement the marker interface (or a capability aggregate like IStandardHttpClientOptions) so the generator can wire this type.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "The INamedHttpClientOptions marker is the contract that opts a type into HttpClient source generation. Types decorated with [HttpClientOptions] but lacking the marker cannot be emitted into the RegisterHttpClients method and must implement the interface (directly or transitively via IStandardHttpClientOptions).",
        helpLinkUri: HelpLinkBase + "NDLRHTTP001.md");

    /// <summary>
    /// NDLRHTTP002: Attribute Name argument conflicts with the ClientName property on the type.
    /// </summary>
    public static readonly DiagnosticDescriptor HttpClientNameSourceConflict = new(
        id: "NDLRHTTP002",
        title: "HttpClient name sources conflict",
        messageFormat: "Type '{0}' has [HttpClientOptions(Name = \"{1}\")] but its ClientName property resolves to \"{2}\". Pick one source — either the attribute argument or the property — and remove the other.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "The HttpClient name can come from the attribute argument, a ClientName property on the type, or type-name inference. When the attribute argument and the property disagree, the generator cannot pick silently. Remove the duplicate source so exactly one is authoritative.",
        helpLinkUri: HelpLinkBase + "NDLRHTTP002.md");

    /// <summary>
    /// NDLRHTTP003: ClientName property body is not a literal expression and no attribute Name override is supplied.
    /// </summary>
    public static readonly DiagnosticDescriptor HttpClientNamePropertyNotLiteral = new(
        id: "NDLRHTTP003",
        title: "ClientName property body is not a literal expression",
        messageFormat: "Type '{0}' has a ClientName property whose body is not a string literal. The generator resolves names at compile time, so either rewrite the body as 'ClientName => \"...\"' or set [HttpClientOptions(Name = \"...\")] on the type.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "HttpClient names must be known at compile time because the generator emits services.AddHttpClient(\"name\", ...) before the DI container is built. ClientName property bodies that compute or compose values cannot be statically evaluated. Use a literal expression body or the attribute Name argument.",
        helpLinkUri: HelpLinkBase + "NDLRHTTP003.md");

    /// <summary>
    /// NDLRHTTP004: Resolved HttpClient name is empty or whitespace.
    /// </summary>
    public static readonly DiagnosticDescriptor HttpClientNameEmpty = new(
        id: "NDLRHTTP004",
        title: "Resolved HttpClient name is empty",
        messageFormat: "Type '{0}' has [HttpClientOptions] but no name source resolves to a non-empty value",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "At least one of the three name sources must yield a non-empty string. This typically happens when the type is literally named HttpClientOptions (suffix stripping leaves nothing) and no attribute argument or property override is provided.",
        helpLinkUri: HelpLinkBase + "NDLRHTTP004.md");

    /// <summary>
    /// NDLRHTTP005: Two [HttpClientOptions] types in the compilation resolve to the same client name.
    /// </summary>
    public static readonly DiagnosticDescriptor HttpClientNameCollision = new(
        id: "NDLRHTTP005",
        title: "Duplicate HttpClient name",
        messageFormat: "Type '{0}' resolves to HttpClient name \"{1}\" which is already used by type '{2}'. HttpClient names must be unique within a compilation.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "services.AddHttpClient(\"name\", ...) registrations are keyed by name. Two options types resolving to the same name would overwrite each other's configuration non-deterministically. Rename one of the types or set an explicit attribute Name argument on one of them.",
        helpLinkUri: HelpLinkBase + "NDLRHTTP005.md",
        customTags: WellKnownDiagnosticTags.CompilationEnd);

    /// <summary>
    /// NDLRHTTP006: ClientName property must be an instance property returning string.
    /// </summary>
    public static readonly DiagnosticDescriptor HttpClientNamePropertyWrongShape = new(
        id: "NDLRHTTP006",
        title: "ClientName property has wrong shape",
        messageFormat: "Type '{0}' has a ClientName property that is not a readable instance property of type 'string'. The generator only recognizes ClientName as 'public string ClientName { get; }' or 'public string ClientName => \"...\";'.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "The ClientName property is one of the three name sources recognized by the generator. To avoid ambiguity, only instance properties of type string with a get accessor are considered. Static, write-only, or non-string ClientName members are rejected so they can't silently fall through to type-name inference.",
        helpLinkUri: HelpLinkBase + "NDLRHTTP006.md");
}
