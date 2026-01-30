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
    /// <param name="methodName">The method name to look for (default: "Validate").</param>
    /// <returns>Validator method info, or null if no validator method found.</returns>
    public static OptionsValidatorMethodInfo? FindValidationMethod(INamedTypeSymbol typeSymbol, string methodName = "Validate")
    {
        // Look for method by name (convention-based)
        foreach (var member in typeSymbol.GetMembers())
        {
            if (member is not IMethodSymbol method)
                continue;

            if (method.Name != methodName)
                continue;

            // Check signature: should return IEnumerable<string> or IEnumerable<ValidationError>
            // Supported signatures:
            // 1. Instance method with no parameters: T.Validate() - for self-validation
            // 2. Static method with one parameter: static T.Validate(TOptions options)
            // 3. Instance method with one parameter: validator.Validate(TOptions options) - for external validators
            if (method.Parameters.Length == 0 && !method.IsStatic)
            {
                // Instance method: T.Validate() - self-validation on options class
                return new OptionsValidatorMethodInfo(method.Name, false);
            }
            
            if (method.Parameters.Length == 1 && method.IsStatic)
            {
                // Static method: T.Validate(T options) - static validator
                return new OptionsValidatorMethodInfo(method.Name, true);
            }

            if (method.Parameters.Length == 1 && !method.IsStatic)
            {
                // Instance method with parameter: validator.Validate(T options) - external validator
                return new OptionsValidatorMethodInfo(method.Name, false);
            }
        }

        return null;
    }

    /// <summary>
    /// Information about a validation method.
    /// </summary>
    public readonly struct OptionsValidatorMethodInfo
    {
        public OptionsValidatorMethodInfo(string methodName, bool isStatic)
        {
            MethodName = methodName;
            IsStatic = isStatic;
        }

        public string MethodName { get; }
        public bool IsStatic { get; }
    }
}
