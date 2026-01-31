using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace NexusLabs.Needlr.Generators.Models;

/// <summary>
/// Information about a discovered injectable type.
/// </summary>
internal readonly struct DiscoveredType
{
    public DiscoveredType(string typeName, string[] interfaceNames, string assemblyName, GeneratorLifetime lifetime, TypeDiscoveryHelper.ConstructorParameterInfo[] constructorParameters, string[] serviceKeys, string? sourceFilePath = null, bool isDisposable = false)
    {
        TypeName = typeName;
        InterfaceNames = interfaceNames;
        AssemblyName = assemblyName;
        Lifetime = lifetime;
        ConstructorParameters = constructorParameters;
        ServiceKeys = serviceKeys;
        SourceFilePath = sourceFilePath;
        IsDisposable = isDisposable;
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
    /// True if this type implements IDisposable or IAsyncDisposable.
    /// </summary>
    public bool IsDisposable { get; }

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
/// Information about a discovered hosted service (BackgroundService or IHostedService implementation).
/// </summary>
internal readonly struct DiscoveredHostedService
{
    public DiscoveredHostedService(
        string typeName,
        string assemblyName,
        GeneratorLifetime lifetime,
        TypeDiscoveryHelper.ConstructorParameterInfo[] constructorParameters,
        string? sourceFilePath = null)
    {
        TypeName = typeName;
        AssemblyName = assemblyName;
        Lifetime = lifetime;
        ConstructorParameters = constructorParameters;
        SourceFilePath = sourceFilePath;
    }

    public string TypeName { get; }
    public string AssemblyName { get; }
    public GeneratorLifetime Lifetime { get; }
    public TypeDiscoveryHelper.ConstructorParameterInfo[] ConstructorParameters { get; }
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
        string? validatorTypeName = null,
        PositionalRecordInfo? positionalRecordInfo = null,
        IReadOnlyList<OptionsPropertyInfo>? properties = null)
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
        PositionalRecordInfo = positionalRecordInfo;
        Properties = properties ?? Array.Empty<OptionsPropertyInfo>();
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

    /// <summary>Information about positional record primary constructor, if applicable.</summary>
    public PositionalRecordInfo? PositionalRecordInfo { get; }

    /// <summary>Bindable properties for AOT code generation.</summary>
    public IReadOnlyList<OptionsPropertyInfo> Properties { get; }

    /// <summary>True if this is a named options registration (not default).</summary>
    public bool IsNamed => Name != null;

    /// <summary>True if this options type has a custom validator method.</summary>
    public bool HasValidatorMethod => ValidatorMethod != null;

    /// <summary>True if an external validator type is specified.</summary>
    public bool HasExternalValidator => ValidatorTypeName != null;

    /// <summary>True if this is a positional record that needs a generated parameterless constructor.</summary>
    public bool NeedsGeneratedConstructor => PositionalRecordInfo?.IsPartial == true;

    /// <summary>True if this is a non-partial positional record (will emit diagnostic).</summary>
    public bool IsNonPartialPositionalRecord => PositionalRecordInfo != null && !PositionalRecordInfo.Value.IsPartial;
    
    /// <summary>True if this type has any init-only properties (requires factory pattern in AOT).</summary>
    public bool HasInitOnlyProperties => Properties.Any(p => p.HasInitOnlySetter);
    
    /// <summary>True if this is a positional record (uses constructor binding in AOT).</summary>
    public bool IsPositionalRecord => PositionalRecordInfo != null;
    
    /// <summary>True if this type requires factory pattern (Options.Create) instead of Configure delegate in AOT.</summary>
    public bool RequiresFactoryPattern => IsPositionalRecord || HasInitOnlyProperties;
    
    /// <summary>True if any property has DataAnnotation validation attributes.</summary>
    public bool HasDataAnnotations => Properties.Any(p => p.HasDataAnnotations);
}

/// <summary>
/// Information about a positional record's primary constructor parameters.
/// </summary>
internal readonly struct PositionalRecordInfo
{
    public PositionalRecordInfo(
        string shortTypeName,
        string containingNamespace,
        bool isPartial,
        IReadOnlyList<PositionalRecordParameter> parameters)
    {
        ShortTypeName = shortTypeName;
        ContainingNamespace = containingNamespace;
        IsPartial = isPartial;
        Parameters = parameters;
    }

    /// <summary>The simple type name (without namespace).</summary>
    public string ShortTypeName { get; }

    /// <summary>The containing namespace.</summary>
    public string ContainingNamespace { get; }

    /// <summary>Whether the record is declared as partial.</summary>
    public bool IsPartial { get; }

    /// <summary>The primary constructor parameters.</summary>
    public IReadOnlyList<PositionalRecordParameter> Parameters { get; }
}

/// <summary>
/// A parameter in a positional record's primary constructor.
/// </summary>
internal readonly struct PositionalRecordParameter
{
    public PositionalRecordParameter(string name, string typeName)
    {
        Name = name;
        TypeName = typeName;
    }

    public string Name { get; }
    public string TypeName { get; }
}

/// <summary>
/// Information about a bindable property on an options class (for AOT code generation).
/// </summary>
internal readonly struct OptionsPropertyInfo
{
    public OptionsPropertyInfo(
        string name, 
        string typeName, 
        bool isNullable, 
        bool hasInitOnlySetter, 
        bool isEnum = false, 
        string? enumTypeName = null,
        ComplexTypeKind complexTypeKind = ComplexTypeKind.None,
        string? elementTypeName = null,
        IReadOnlyList<OptionsPropertyInfo>? nestedProperties = null,
        IReadOnlyList<DataAnnotationInfo>? dataAnnotations = null)
    {
        Name = name;
        TypeName = typeName;
        IsNullable = isNullable;
        HasInitOnlySetter = hasInitOnlySetter;
        IsEnum = isEnum;
        EnumTypeName = enumTypeName;
        ComplexTypeKind = complexTypeKind;
        ElementTypeName = elementTypeName;
        NestedProperties = nestedProperties;
        DataAnnotations = dataAnnotations ?? Array.Empty<DataAnnotationInfo>();
    }

    /// <summary>Property name.</summary>
    public string Name { get; }

    /// <summary>Fully qualified type name.</summary>
    public string TypeName { get; }

    /// <summary>True if the property type is nullable.</summary>
    public bool IsNullable { get; }

    /// <summary>True if the property has an init-only setter.</summary>
    public bool HasInitOnlySetter { get; }

    /// <summary>True if the property type is an enum.</summary>
    public bool IsEnum { get; }

    /// <summary>The underlying enum type name (for nullable enums, this is the non-nullable type).</summary>
    public string? EnumTypeName { get; }
    
    /// <summary>The kind of complex type (nested object, array, list, dictionary).</summary>
    public ComplexTypeKind ComplexTypeKind { get; }
    
    /// <summary>For collections, the element type. For dictionaries, the value type.</summary>
    public string? ElementTypeName { get; }
    
    /// <summary>For nested objects and collection element types, the bindable properties.</summary>
    public IReadOnlyList<OptionsPropertyInfo>? NestedProperties { get; }
    
    /// <summary>DataAnnotation validation attributes on this property.</summary>
    public IReadOnlyList<DataAnnotationInfo> DataAnnotations { get; }
    
    /// <summary>True if this property has any DataAnnotation validation attributes.</summary>
    public bool HasDataAnnotations => DataAnnotations.Count > 0;
}

/// <summary>
/// Identifies the kind of complex type for AOT binding generation.
/// </summary>
internal enum ComplexTypeKind
{
    /// <summary>Not a complex type (primitive, enum, etc).</summary>
    None,
    /// <summary>A nested object with properties to bind.</summary>
    NestedObject,
    /// <summary>An array type (T[]).</summary>
    Array,
    /// <summary>A list type (List&lt;T&gt;, IList&lt;T&gt;, etc).</summary>
    List,
    /// <summary>A dictionary type (Dictionary&lt;string, T&gt;).</summary>
    Dictionary
}

/// <summary>
/// Identifies the kind of DataAnnotation attribute for source-generated validation.
/// </summary>
internal enum DataAnnotationKind
{
    Required,
    Range,
    StringLength,
    MinLength,
    MaxLength,
    RegularExpression,
    EmailAddress,
    Phone,
    Url,
    Unsupported
}

/// <summary>
/// Information about a DataAnnotation attribute on an options property.
/// </summary>
internal readonly struct DataAnnotationInfo
{
    public DataAnnotationInfo(
        DataAnnotationKind kind,
        string? errorMessage = null,
        object? minimum = null,
        object? maximum = null,
        string? pattern = null,
        int? minimumLength = null)
    {
        Kind = kind;
        ErrorMessage = errorMessage;
        Minimum = minimum;
        Maximum = maximum;
        Pattern = pattern;
        MinimumLength = minimumLength;
    }

    /// <summary>The kind of DataAnnotation attribute.</summary>
    public DataAnnotationKind Kind { get; }
    
    /// <summary>Custom error message if specified.</summary>
    public string? ErrorMessage { get; }
    
    /// <summary>Minimum value for Range attribute.</summary>
    public object? Minimum { get; }
    
    /// <summary>Maximum value for Range/StringLength/MaxLength attributes.</summary>
    public object? Maximum { get; }
    
    /// <summary>Pattern for RegularExpression attribute.</summary>
    public string? Pattern { get; }
    
    /// <summary>Minimum length for StringLength/MinLength attributes.</summary>
    public int? MinimumLength { get; }
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
        IReadOnlyList<DiscoveredOptions> options,
        IReadOnlyList<DiscoveredHostedService> hostedServices)
    {
        InjectableTypes = injectableTypes;
        PluginTypes = pluginTypes;
        Decorators = decorators;
        InaccessibleTypes = inaccessibleTypes;
        MissingTypeRegistryPlugins = missingTypeRegistryPlugins;
        InterceptedServices = interceptedServices;
        Factories = factories;
        Options = options;
        HostedServices = hostedServices;
    }

    public IReadOnlyList<DiscoveredType> InjectableTypes { get; }
    public IReadOnlyList<DiscoveredPlugin> PluginTypes { get; }
    public IReadOnlyList<DiscoveredDecorator> Decorators { get; }
    public IReadOnlyList<InaccessibleType> InaccessibleTypes { get; }
    public IReadOnlyList<MissingTypeRegistryPlugin> MissingTypeRegistryPlugins { get; }
    public IReadOnlyList<DiscoveredInterceptedService> InterceptedServices { get; }
    public IReadOnlyList<DiscoveredFactory> Factories { get; }
    public IReadOnlyList<DiscoveredOptions> Options { get; }
    public IReadOnlyList<DiscoveredHostedService> HostedServices { get; }
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
