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

    /// <summary>
    /// NDLRCOR005: Lifetime mismatch - longer-lived service depends on shorter-lived service.
    /// </summary>
    public const string LifetimeMismatch = "NDLRCOR005";

    /// <summary>
    /// NDLRCOR006: Circular dependency detected in service registration.
    /// </summary>
    public const string CircularDependency = "NDLRCOR006";

    /// <summary>
    /// NDLRCOR007: Intercept attribute type must implement IMethodInterceptor.
    /// </summary>
    public const string InterceptTypeMustImplementInterface = "NDLRCOR007";

    /// <summary>
    /// NDLRCOR008: [Intercept] applied to class without interfaces.
    /// </summary>
    public const string InterceptOnClassWithoutInterfaces = "NDLRCOR008";

    /// <summary>
    /// NDLRCOR009: Lazy&lt;T&gt; references type not discovered by source generation.
    /// </summary>
    public const string LazyResolutionUnknown = "NDLRCOR009";

    /// <summary>
    /// NDLRCOR010: IEnumerable&lt;T&gt; has no implementations discovered by source generation.
    /// </summary>
    public const string CollectionResolutionEmpty = "NDLRCOR010";

    /// <summary>
    /// NDLRCOR011: [FromKeyedServices] references a key with no known registration.
    /// </summary>
    public const string KeyedServiceUnknownKey = "NDLRCOR011";

    /// <summary>
    /// NDLRCOR012: [GenerateFactory] on type with all injectable parameters is unnecessary.
    /// </summary>
    public const string FactoryAllParamsInjectable = "NDLRCOR012";

    /// <summary>
    /// NDLRCOR013: [GenerateFactory] on type with no injectable parameters provides low value.
    /// </summary>
    public const string FactoryNoInjectableParams = "NDLRCOR013";

    /// <summary>
    /// NDLRCOR014: [GenerateFactory&lt;T&gt;] type argument must be an interface implemented by the class.
    /// </summary>
    public const string FactoryTypeArgNotImplemented = "NDLRCOR014";

    /// <summary>
    /// NDLRCOR015: [RegisterAs&lt;T&gt;] type argument must be an interface implemented by the class.
    /// </summary>
    public const string RegisterAsTypeArgNotImplemented = "NDLRCOR015";
}
