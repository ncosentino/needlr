using Microsoft.CodeAnalysis;

namespace NexusLabs.Needlr.Generators;

/// <summary>
/// Helper for discovering Options attributes from Roslyn symbols.
/// </summary>
internal static class OptionsAttributeHelper
{
    private const string OptionsAttributeName = "OptionsAttribute";
    private const string OptionsAttributeFullName = "NexusLabs.Needlr.OptionsAttribute";

    /// <summary>
    /// Information extracted from an [Options] attribute.
    /// </summary>
    public readonly struct OptionsAttributeInfo
    {
        public OptionsAttributeInfo(string? sectionName, string? name, bool validateOnStart, string? validateMethod = null, INamedTypeSymbol? validatorType = null)
        {
            SectionName = sectionName;
            Name = name;
            ValidateOnStart = validateOnStart;
            ValidateMethod = validateMethod;
            ValidatorType = validatorType;
        }

        /// <summary>Explicit section name from attribute, or null to infer from class name.</summary>
        public string? SectionName { get; }

        /// <summary>Named options name (e.g., "Primary"), or null for default options.</summary>
        public string? Name { get; }

        /// <summary>Whether to validate options on startup.</summary>
        public bool ValidateOnStart { get; }

        /// <summary>Custom validation method name, or null to use convention ("Validate").</summary>
        public string? ValidateMethod { get; }

        /// <summary>External validator type, or null to use the options class itself.</summary>
        public INamedTypeSymbol? ValidatorType { get; }
    }

    /// <summary>
    /// Checks if a type has the [Options] attribute.
    /// </summary>
    /// <param name="typeSymbol">The type symbol to check.</param>
    /// <returns>True if the type has [Options]; otherwise, false.</returns>
    public static bool HasOptionsAttribute(INamedTypeSymbol typeSymbol)
    {
        foreach (var attribute in typeSymbol.GetAttributes())
        {
            var attributeClass = attribute.AttributeClass;
            if (attributeClass == null)
                continue;

            var name = attributeClass.Name;
            if (name == OptionsAttributeName)
                return true;

            var fullName = attributeClass.ToDisplayString();
            if (fullName == OptionsAttributeFullName)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Gets all [Options] attribute data from a type.
    /// </summary>
    /// <param name="typeSymbol">The type symbol to check.</param>
    /// <returns>A list of options attribute info for each [Options] on the type.</returns>
    public static IReadOnlyList<OptionsAttributeInfo> GetOptionsAttributes(INamedTypeSymbol typeSymbol)
    {
        var result = new List<OptionsAttributeInfo>();

        foreach (var attribute in typeSymbol.GetAttributes())
        {
            var attributeClass = attribute.AttributeClass;
            if (attributeClass == null)
                continue;

            var name = attributeClass.Name;
            var fullName = attributeClass.ToDisplayString();

            if (name != OptionsAttributeName && fullName != OptionsAttributeFullName)
                continue;

            // Extract constructor argument (optional section name)
            string? sectionName = null;
            if (attribute.ConstructorArguments.Length > 0 &&
                attribute.ConstructorArguments[0].Value is string section)
            {
                sectionName = section;
            }

            // Extract named arguments
            string? optionsName = null;
            bool validateOnStart = false;
            string? validateMethod = null;
            INamedTypeSymbol? validatorType = null;

            foreach (var namedArg in attribute.NamedArguments)
            {
                if (namedArg.Key == "Name" && namedArg.Value.Value is string n)
                {
                    optionsName = n;
                }
                else if (namedArg.Key == "ValidateOnStart" && namedArg.Value.Value is bool v)
                {
                    validateOnStart = v;
                }
                else if (namedArg.Key == "ValidateMethod" && namedArg.Value.Value is string vm)
                {
                    validateMethod = vm;
                }
                else if (namedArg.Key == "Validator" && namedArg.Value.Value is INamedTypeSymbol vt)
                {
                    validatorType = vt;
                }
            }

            result.Add(new OptionsAttributeInfo(sectionName, optionsName, validateOnStart, validateMethod, validatorType));
        }

        return result;
    }

    /// <summary>
    /// Finds a validation method on a type by convention or explicit name.
    /// Convention: method named "Validate" (or custom name via ValidateMethod property).
    /// </summary>
    /// <param name="typeSymbol">The type symbol to search.</param>
    /// <param name="optionsType">The options type accepted by the validation method.</param>
    /// <param name="methodName">The method name to look for.</param>
    /// <param name="isExternalValidator">Whether the target type is an external validator.</param>
    /// <param name="allowInterfaceFallback">
    /// Whether a matching <c>IOptionsValidator&lt;TOptions&gt;</c> implementation may be used
    /// when no public validation method is available.
    /// </param>
    /// <returns>Validator method info, or null if no validator method found.</returns>
    internal static OptionsValidatorMethodInfo? FindValidationMethod(
        INamedTypeSymbol typeSymbol,
        INamedTypeSymbol optionsType,
        string methodName,
        bool isExternalValidator,
        bool allowInterfaceFallback)
    {
        foreach (var method in GetValidationMethods(typeSymbol, methodName))
        {
            if (GetValidationMethodSignatureError(
                    method,
                    optionsType,
                    isExternalValidator) is not null)
            {
                continue;
            }

            if (isExternalValidator &&
                !SymbolEqualityComparer.Default.Equals(
                    method.Parameters[0].Type,
                    optionsType))
            {
                continue;
            }

            return new OptionsValidatorMethodInfo(
                method.Name,
                method.IsStatic,
                usesOptionsValidatorInterface: false);
        }

        if (isExternalValidator &&
            allowInterfaceFallback &&
            GetIOptionsValidatorTypeArguments(typeSymbol).Any(typeArgument =>
                SymbolEqualityComparer.Default.Equals(typeArgument, optionsType)))
        {
            return new OptionsValidatorMethodInfo(
                "Validate",
                isStatic: false,
                usesOptionsValidatorInterface: true);
        }

        return null;
    }

    internal static IEnumerable<IMethodSymbol> GetValidationMethods(
        INamedTypeSymbol targetType,
        string methodName)
    {
        foreach (var member in targetType.GetMembers())
        {
            if (member is IMethodSymbol method &&
                method.Name == methodName &&
                method.DeclaredAccessibility == Accessibility.Public &&
                method.MethodKind == MethodKind.Ordinary)
            {
                yield return method;
            }
        }
    }

    internal static string? GetValidationMethodSignatureError(
        IMethodSymbol method,
        INamedTypeSymbol optionsType,
        bool isExternalValidator)
    {
        if (method.ReturnType is not INamedTypeSymbol returnType ||
            returnType.OriginalDefinition.ToDisplayString() !=
                "System.Collections.Generic.IEnumerable<T>" ||
            returnType.TypeArguments.Length != 1)
        {
            return "IEnumerable<ValidationError> or IEnumerable<string>";
        }

        var resultType = returnType.TypeArguments[0];
        if (resultType.SpecialType != SpecialType.System_String &&
            resultType.ToDisplayString() !=
                "NexusLabs.Needlr.Generators.ValidationError")
        {
            return "IEnumerable<ValidationError> or IEnumerable<string>";
        }

        if (isExternalValidator)
        {
            return method.Parameters.Length == 1
                ? null
                : $"IEnumerable<ValidationError> {method.Name}({optionsType.Name} options)";
        }

        if (!method.IsStatic && method.Parameters.Length != 0)
        {
            return $"IEnumerable<ValidationError> {method.Name}()";
        }

        if (method.IsStatic &&
            (method.Parameters.Length != 1 ||
             !SymbolEqualityComparer.Default.Equals(
                 method.Parameters[0].Type,
                 optionsType)))
        {
            return $"static IEnumerable<ValidationError> {method.Name}({optionsType.Name} options)";
        }

        return null;
    }

    internal static IEnumerable<ITypeSymbol> GetIOptionsValidatorTypeArguments(
        INamedTypeSymbol validatorType)
    {
        foreach (var iface in validatorType.AllInterfaces)
        {
            if (iface.Name == "IOptionsValidator" &&
                iface.ContainingNamespace?.ToDisplayString() ==
                    "NexusLabs.Needlr.Generators" &&
                iface.IsGenericType &&
                iface.TypeArguments.Length == 1)
            {
                yield return iface.TypeArguments[0];
            }
        }
    }

    /// <summary>
    /// Information about a validation method.
    /// </summary>
    public readonly struct OptionsValidatorMethodInfo
    {
        public OptionsValidatorMethodInfo(
            string methodName,
            bool isStatic,
            bool usesOptionsValidatorInterface)
        {
            MethodName = methodName;
            IsStatic = isStatic;
            UsesOptionsValidatorInterface = usesOptionsValidatorInterface;
        }

        public string MethodName { get; }
        public bool IsStatic { get; }
        public bool UsesOptionsValidatorInterface { get; }
    }
}
