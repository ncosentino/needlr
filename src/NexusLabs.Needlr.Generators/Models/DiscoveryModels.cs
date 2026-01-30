using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace NexusLabs.Needlr.Generators.Models;

/// <summary>
/// Information about a discovered injectable type.
/// </summary>
internal readonly struct DiscoveredType
{
    public DiscoveredType(string typeName, string[] interfaceNames, string assemblyName, GeneratorLifetime lifetime, TypeDiscoveryHelper.ConstructorParameterInfo[] constructorParameters, string[] serviceKeys, string? sourceFilePath = null)
    {
        TypeName = typeName;
        InterfaceNames = interfaceNames;
        AssemblyName = assemblyName;
        Lifetime = lifetime;
        ConstructorParameters = constructorParameters;
        ServiceKeys = serviceKeys;
        SourceFilePath = sourceFilePath;
    }

    public string TypeName { get; }
    public string[] InterfaceNames { get; }
    public string AssemblyName { get; }
    public GeneratorLifetime Lifetime { get; }
    public TypeDiscoveryHelper.ConstructorParameterInfo[] ConstructorParameters { get; }
    /// <summary>
    /// Service keys from [Keyed] attributes on this type.
    /// </summary>
    public string[] ServiceKeys { get; }
    public string? SourceFilePath { get; }

    /// <summary>
    /// Gets the constructor parameter types (for backward compatibility with existing code paths).
    /// </summary>
    public string[] ConstructorParameterTypes => ConstructorParameters.Select(p => p.TypeName).ToArray();

    /// <summary>
    /// True if any constructor parameters are keyed services.
    /// </summary>
    public bool HasKeyedParameters => ConstructorParameters.Any(p => p.IsKeyed);

    /// <summary>
    /// True if this type has [Keyed] attributes for keyed registration.
    /// </summary>
    public bool IsKeyed => ServiceKeys.Length > 0;
}

/// <summary>
/// Information about a discovered plugin type (implements INeedlrPlugin interfaces).
/// </summary>
internal readonly struct DiscoveredPlugin
{
    public DiscoveredPlugin(string typeName, string[] interfaceNames, string assemblyName, string[] attributeNames, string? sourceFilePath = null, int order = 0)
    {
        TypeName = typeName;
        InterfaceNames = interfaceNames;
        AssemblyName = assemblyName;
        AttributeNames = attributeNames;
        SourceFilePath = sourceFilePath;
        Order = order;
    }

    public string TypeName { get; }
    public string[] InterfaceNames { get; }
    public string AssemblyName { get; }
    public string[] AttributeNames { get; }
    public string? SourceFilePath { get; }
    public int Order { get; }
}

/// <summary>
/// Information about a closed-generic decorator (from [DecoratorFor&lt;T&gt;]).
/// </summary>
internal readonly struct DiscoveredDecorator
{
    public DiscoveredDecorator(string decoratorTypeName, string serviceTypeName, int order, string assemblyName, string? sourceFilePath = null)
    {
        DecoratorTypeName = decoratorTypeName;
        ServiceTypeName = serviceTypeName;
        Order = order;
        AssemblyName = assemblyName;
        SourceFilePath = sourceFilePath;
    }

    public string DecoratorTypeName { get; }
    public string ServiceTypeName { get; }
    public int Order { get; }
    public string AssemblyName { get; }
    public string? SourceFilePath { get; }
}

/// <summary>
/// Information about an open-generic decorator (from [OpenDecoratorFor(typeof(IHandler&lt;&gt;))]).
/// </summary>
internal readonly struct DiscoveredOpenDecorator
{
    public DiscoveredOpenDecorator(
        INamedTypeSymbol decoratorType,
        INamedTypeSymbol openGenericInterface,
        int order,
        string assemblyName,
        string? sourceFilePath = null)
    {
        DecoratorType = decoratorType;
        OpenGenericInterface = openGenericInterface;
        Order = order;
        AssemblyName = assemblyName;
        SourceFilePath = sourceFilePath;
    }

    public INamedTypeSymbol DecoratorType { get; }
    public INamedTypeSymbol OpenGenericInterface { get; }
    public int Order { get; }
    public string AssemblyName { get; }
    public string? SourceFilePath { get; }
}

/// <summary>
/// Information about a type that would be injectable but is inaccessible (internal/private).
/// </summary>
internal readonly struct InaccessibleType
{
    public InaccessibleType(string typeName, string assemblyName)
    {
        TypeName = typeName;
        AssemblyName = assemblyName;
    }

    public string TypeName { get; }
    public string AssemblyName { get; }
}

/// <summary>
/// Information about a plugin type from a referenced assembly that's missing [GenerateTypeRegistry].
/// </summary>
internal readonly struct MissingTypeRegistryPlugin
{
    public MissingTypeRegistryPlugin(string typeName, string assemblyName)
    {
        TypeName = typeName;
        AssemblyName = assemblyName;
    }

    public string TypeName { get; }
    public string AssemblyName { get; }
}

/// <summary>
/// Information about an intercepted service (from [Intercept&lt;T&gt;]).
/// </summary>
internal readonly struct DiscoveredInterceptedService
{
    public DiscoveredInterceptedService(
        string typeName,
        string[] interfaceNames,
        string assemblyName,
        GeneratorLifetime lifetime,
        InterceptorDiscoveryHelper.InterceptedMethodInfo[] methods,
        string[] allInterceptorTypeNames,
        string? sourceFilePath = null)
    {
        TypeName = typeName;
        InterfaceNames = interfaceNames;
        AssemblyName = assemblyName;
        Lifetime = lifetime;
        Methods = methods;
        AllInterceptorTypeNames = allInterceptorTypeNames;
        SourceFilePath = sourceFilePath;
    }

    public string TypeName { get; }
    public string[] InterfaceNames { get; }
    public string AssemblyName { get; }
    public GeneratorLifetime Lifetime { get; }
    public InterceptorDiscoveryHelper.InterceptedMethodInfo[] Methods { get; }
    public string[] AllInterceptorTypeNames { get; }
    public string? SourceFilePath { get; }
}

/// <summary>
/// Information about a factory-generated type (from [GenerateFactory]).
/// </summary>
internal readonly struct DiscoveredFactory
{
    public DiscoveredFactory(
        string typeName,
        string[] interfaceNames,
        string assemblyName,
        int generationMode,
        FactoryDiscoveryHelper.FactoryConstructorInfo[] constructors,
        string? returnTypeName = null,
        string? sourceFilePath = null)
    {
        TypeName = typeName;
        InterfaceNames = interfaceNames;
        AssemblyName = assemblyName;
        GenerationMode = generationMode;
        Constructors = constructors;
        ReturnTypeOverride = returnTypeName;
        SourceFilePath = sourceFilePath;
    }

    public string TypeName { get; }
    public string[] InterfaceNames { get; }
    public string AssemblyName { get; }
    /// <summary>Mode flags: 1=Func, 2=Interface, 3=All</summary>
    public int GenerationMode { get; }
    public FactoryDiscoveryHelper.FactoryConstructorInfo[] Constructors { get; }
    /// <summary>
    /// If set, the factory Create() and Func return this type instead of the concrete type.
    /// Used when [GenerateFactory&lt;T&gt;] is applied.
    /// </summary>
    public string? ReturnTypeOverride { get; }
    public string? SourceFilePath { get; }

    public bool GenerateFunc => (GenerationMode & 1) != 0;
    public bool GenerateInterface => (GenerationMode & 2) != 0;

    /// <summary>Gets the type that factory Create() and Func should return.</summary>
    public string ReturnTypeName => ReturnTypeOverride ?? TypeName;

    /// <summary>Gets just the type name without namespace (e.g., "MyService" from "global::TestApp.MyService").</summary>
    public string SimpleTypeName
    {
        get
        {
            var parts = TypeName.Split('.');
            return parts[parts.Length - 1];
        }
    }
}

/// <summary>
/// Information about a discovered options type (from [Options]).
/// </summary>
internal readonly struct DiscoveredOptions
{
    public DiscoveredOptions(
        string typeName,
        string sectionName,
        string? name,
        bool validateOnStart,
        string assemblyName,
        string? sourceFilePath = null,
        OptionsValidatorInfo? validatorMethod = null,
        string? validateMethodOverride = null,
        string? validatorTypeName = null)
    {
        TypeName = typeName;
        SectionName = sectionName;
        Name = name;
        ValidateOnStart = validateOnStart;
        AssemblyName = assemblyName;
        SourceFilePath = sourceFilePath;
        ValidatorMethod = validatorMethod;
        ValidateMethodOverride = validateMethodOverride;
        ValidatorTypeName = validatorTypeName;
    }

    /// <summary>Fully qualified type name of the options class.</summary>
    public string TypeName { get; }

    /// <summary>Configuration section name (e.g., "Database").</summary>
    public string SectionName { get; }

    /// <summary>Named options name (e.g., "Primary"), or null for default options.</summary>
    public string? Name { get; }

    /// <summary>Whether to validate options on startup.</summary>
    public bool ValidateOnStart { get; }

    public string AssemblyName { get; }
    public string? SourceFilePath { get; }

    /// <summary>Information about the validation method (discovered or specified).</summary>
    public OptionsValidatorInfo? ValidatorMethod { get; }

    /// <summary>Custom validation method name override from [Options(ValidateMethod = "...")], or null to use convention.</summary>
    public string? ValidateMethodOverride { get; }

    /// <summary>External validator type name from [Options(Validator = typeof(...))], or null to use options class.</summary>
    public string? ValidatorTypeName { get; }

    /// <summary>True if this is a named options registration (not default).</summary>
    public bool IsNamed => Name != null;

    /// <summary>True if this options type has a custom validator method.</summary>
    public bool HasValidatorMethod => ValidatorMethod != null;

    /// <summary>True if an external validator type is specified.</summary>
    public bool HasExternalValidator => ValidatorTypeName != null;
}

/// <summary>
/// Information about a validation method.
/// </summary>
internal readonly struct OptionsValidatorInfo
{
    public OptionsValidatorInfo(string methodName, bool isStatic)
    {
        MethodName = methodName;
        IsStatic = isStatic;
    }

    /// <summary>Name of the validator method.</summary>
    public string MethodName { get; }

    /// <summary>True if the method is static.</summary>
    public bool IsStatic { get; }
}

/// <summary>
/// Aggregated result of type discovery for an assembly.
/// </summary>
internal readonly struct DiscoveryResult
{
    public DiscoveryResult(
        IReadOnlyList<DiscoveredType> injectableTypes,
        IReadOnlyList<DiscoveredPlugin> pluginTypes,
        IReadOnlyList<DiscoveredDecorator> decorators,
        IReadOnlyList<InaccessibleType> inaccessibleTypes,
        IReadOnlyList<MissingTypeRegistryPlugin> missingTypeRegistryPlugins,
        IReadOnlyList<DiscoveredInterceptedService> interceptedServices,
        IReadOnlyList<DiscoveredFactory> factories,
        IReadOnlyList<DiscoveredOptions> options)
    {
        InjectableTypes = injectableTypes;
        PluginTypes = pluginTypes;
        Decorators = decorators;
        InaccessibleTypes = inaccessibleTypes;
        MissingTypeRegistryPlugins = missingTypeRegistryPlugins;
        InterceptedServices = interceptedServices;
        Factories = factories;
        Options = options;
    }

    public IReadOnlyList<DiscoveredType> InjectableTypes { get; }
    public IReadOnlyList<DiscoveredPlugin> PluginTypes { get; }
    public IReadOnlyList<DiscoveredDecorator> Decorators { get; }
    public IReadOnlyList<InaccessibleType> InaccessibleTypes { get; }
    public IReadOnlyList<MissingTypeRegistryPlugin> MissingTypeRegistryPlugins { get; }
    public IReadOnlyList<DiscoveredInterceptedService> InterceptedServices { get; }
    public IReadOnlyList<DiscoveredFactory> Factories { get; }
    public IReadOnlyList<DiscoveredOptions> Options { get; }
}

/// <summary>
/// Information parsed from [GenerateTypeRegistry] attribute.
/// </summary>
internal readonly struct AttributeInfo
{
    public AttributeInfo(string[]? namespacePrefixes, bool includeSelf)
    {
        NamespacePrefixes = namespacePrefixes;
        IncludeSelf = includeSelf;
    }

    public string[]? NamespacePrefixes { get; }
    public bool IncludeSelf { get; }
}
