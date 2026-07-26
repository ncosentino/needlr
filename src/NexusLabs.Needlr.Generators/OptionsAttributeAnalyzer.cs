// Copyright (c) NexusLabs. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace NexusLabs.Needlr.Generators;

/// <summary>
/// Analyzer that validates [Options] attribute usage for validation configuration:
/// - NDLRGEN014: Validator type has no validation method
/// - NDLRGEN015: Validator type mismatch
/// - NDLRGEN016: Validation method not found
/// - NDLRGEN017: Validation method has wrong signature
/// - NDLRGEN018: Validator won't run (ValidateOnStart = false)
/// - NDLRGEN019: ValidateMethod won't run (ValidateOnStart = false)
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class OptionsAttributeAnalyzer : DiagnosticAnalyzer
{
    private const string OptionsAttributeName = "OptionsAttribute";
    private const string GeneratorsNamespace = "NexusLabs.Needlr.Generators";
    private const string IOptionsValidatorName = "IOptionsValidator";

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(
            DiagnosticDescriptors.ValidatorTypeMissingInterface,
            DiagnosticDescriptors.ValidatorTypeMismatch,
            DiagnosticDescriptors.ValidateMethodNotFound,
            DiagnosticDescriptors.ValidateMethodWrongSignature,
            DiagnosticDescriptors.ValidatorWontRun,
            DiagnosticDescriptors.ValidateMethodWontRun);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterSyntaxNodeAction(AnalyzeOptionsAttribute, SyntaxKind.Attribute);
    }

    private static void AnalyzeOptionsAttribute(SyntaxNodeAnalysisContext context)
    {
        var attributeSyntax = (AttributeSyntax)context.Node;
        var attributeSymbol = context.SemanticModel.GetSymbolInfo(attributeSyntax).Symbol?.ContainingType;

        if (attributeSymbol == null)
            return;

        // Check if this is an [Options] attribute
        if (!IsOptionsAttribute(attributeSymbol))
            return;

        // Get the type this attribute is applied to
        var typeDeclaration = attributeSyntax.Parent?.Parent as TypeDeclarationSyntax;
        if (typeDeclaration == null)
            return;

        var optionsType = context.SemanticModel.GetDeclaredSymbol(typeDeclaration);
        if (optionsType == null)
            return;

        // Extract attribute properties
        var attributeData = optionsType.GetAttributes()
            .FirstOrDefault(a => IsOptionsAttribute(a.AttributeClass));

        if (attributeData == null)
            return;

        bool validateOnStart = false;
        string? validateMethod = null;
        INamedTypeSymbol? validatorType = null;

        foreach (var namedArg in attributeData.NamedArguments)
        {
            switch (namedArg.Key)
            {
                case "ValidateOnStart":
                    validateOnStart = namedArg.Value.Value is true;
                    break;
                case "ValidateMethod":
                    validateMethod = namedArg.Value.Value as string;
                    break;
                case "Validator":
                    validatorType = namedArg.Value.Value as INamedTypeSymbol;
                    break;
            }
        }

        // NDLRGEN018: Validator specified but ValidateOnStart is false
        if (validatorType != null && !validateOnStart)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.ValidatorWontRun,
                attributeSyntax.GetLocation(),
                validatorType.Name));
        }

        // NDLRGEN019: ValidateMethod specified but ValidateOnStart is false
        if (validateMethod != null && !validateOnStart)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.ValidateMethodWontRun,
                attributeSyntax.GetLocation(),
                validateMethod));
        }

        // If ValidateOnStart is true, validate the configuration
        if (validateOnStart)
        {
            var targetType = validatorType ?? optionsType;
            var methodName = validateMethod ?? "Validate";

            // Check if validator is recognized by an extension (e.g., FluentValidation)
            // If so, skip our method signature checks - the extension handles it
            var isRecognizedByExtension = validatorType != null && IsRecognizedByValidatorProvider(validatorType, context.Compilation);
            if (isRecognizedByExtension)
                return;

            var validationMethods = FindValidationMethods(targetType, methodName).ToArray();
            var validMethod = validationMethods.FirstOrDefault(method =>
                ValidateMethodSignature(method, optionsType, validatorType != null) == null &&
                (validatorType == null ||
                 SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, optionsType)));

            if (validMethod != null)
                return;

            if (validationMethods.Length > 0)
            {
                var validationMethod = validationMethods[0];
                var signatureError = ValidateMethodSignature(validationMethod, optionsType, validatorType != null);
                if (signatureError != null)
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        DiagnosticDescriptors.ValidateMethodWrongSignature,
                        attributeSyntax.GetLocation(),
                        methodName,
                        targetType.Name,
                        signatureError));
                }
                else if (validatorType != null)
                {
                    var parameterType = validationMethod.Parameters[0].Type;
                    context.ReportDiagnostic(Diagnostic.Create(
                        DiagnosticDescriptors.ValidatorTypeMismatch,
                        attributeSyntax.GetLocation(),
                        validatorType.Name,
                        parameterType.Name,
                        optionsType.Name));
                }

                return;
            }

            if (validatorType != null && validateMethod == null)
            {
                var interfaceTypeArguments = GetIOptionsValidatorTypeArguments(validatorType).ToArray();
                if (interfaceTypeArguments.Any(typeArgument =>
                    SymbolEqualityComparer.Default.Equals(typeArgument, optionsType)))
                {
                    return;
                }

                if (interfaceTypeArguments.Length > 0)
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        DiagnosticDescriptors.ValidatorTypeMismatch,
                        attributeSyntax.GetLocation(),
                        validatorType.Name,
                        interfaceTypeArguments[0].Name,
                        optionsType.Name));
                    return;
                }

                context.ReportDiagnostic(Diagnostic.Create(
                    DiagnosticDescriptors.ValidatorTypeMissingInterface,
                    attributeSyntax.GetLocation(),
                    validatorType.Name,
                    optionsType.Name));
                return;
            }

            // Convention-based self-validation is optional.
            if (validateMethod != null)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    DiagnosticDescriptors.ValidateMethodNotFound,
                    attributeSyntax.GetLocation(),
                    methodName,
                    targetType.Name));
            }
        }
    }

    private static bool IsOptionsAttribute(INamedTypeSymbol? attributeClass)
    {
        if (attributeClass == null)
            return false;

        return attributeClass.Name == OptionsAttributeName &&
               attributeClass.ContainingNamespace?.ToDisplayString() == GeneratorsNamespace;
    }

    private static IEnumerable<IMethodSymbol> FindValidationMethods(
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

    private static string? ValidateMethodSignature(IMethodSymbol method, INamedTypeSymbol optionsType, bool isExternalValidator)
    {
        if (method.ReturnType is not INamedTypeSymbol returnType ||
            returnType.OriginalDefinition.ToDisplayString() != "System.Collections.Generic.IEnumerable<T>" ||
            returnType.TypeArguments.Length != 1)
        {
            return "IEnumerable<ValidationError> or IEnumerable<string>";
        }

        var resultType = returnType.TypeArguments[0];
        if (resultType.SpecialType != SpecialType.System_String &&
            resultType.ToDisplayString() != "NexusLabs.Needlr.Generators.ValidationError")
        {
            return "IEnumerable<ValidationError> or IEnumerable<string>";
        }

        // Check parameters
        if (isExternalValidator)
        {
            // External validator should have one parameter of the options type
            if (method.Parameters.Length != 1)
            {
                return $"IEnumerable<ValidationError> {method.Name}({optionsType.Name} options)";
            }
        }
        else
        {
            // Self-validation should have no parameters (unless static with one param)
            if (!method.IsStatic && method.Parameters.Length != 0)
            {
                return $"IEnumerable<ValidationError> {method.Name}()";
            }

            if (method.IsStatic && method.Parameters.Length != 1)
            {
                return $"static IEnumerable<ValidationError> {method.Name}({optionsType.Name} options)";
            }

            if (method.IsStatic &&
                !SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, optionsType))
            {
                return $"static IEnumerable<ValidationError> {method.Name}({optionsType.Name} options)";
            }
        }

        return null; // Valid signature
    }

    private static IEnumerable<ITypeSymbol> GetIOptionsValidatorTypeArguments(
        INamedTypeSymbol validatorType)
    {
        foreach (var iface in validatorType.AllInterfaces)
        {
            if (iface.Name == IOptionsValidatorName &&
                iface.ContainingNamespace?.ToDisplayString() == GeneratorsNamespace &&
                iface.IsGenericType &&
                iface.TypeArguments.Length == 1)
            {
                yield return iface.TypeArguments[0];
            }
        }
    }

    private static bool IsRecognizedByValidatorProvider(INamedTypeSymbol validatorType, Compilation compilation)
    {
        // Collect all ValidatorProvider attributes from all referenced assemblies
        var validatorBaseTypes = new HashSet<string>();

        foreach (var reference in compilation.References)
        {
            if (compilation.GetAssemblyOrModuleSymbol(reference) is not IAssemblySymbol assemblySymbol)
                continue;

            foreach (var attr in assemblySymbol.GetAttributes())
            {
                if (attr.AttributeClass?.Name != "ValidatorProviderAttribute")
                    continue;
                if (attr.AttributeClass.ContainingNamespace?.ToDisplayString() != GeneratorsNamespace)
                    continue;

                if (attr.ConstructorArguments.Length > 0 &&
                    attr.ConstructorArguments[0].Value is string baseTypeName)
                {
                    validatorBaseTypes.Add(baseTypeName);
                }
            }
        }

        // Check if validatorType inherits from any recognized base
        return validatorBaseTypes.Any(baseTypeName =>
            InheritsFromByMetadataName(validatorType, baseTypeName));
    }

    private static bool InheritsFromByMetadataName(INamedTypeSymbol type, string metadataName)
    {
        var current = type.BaseType;
        while (current != null)
        {
            var fullName = current.OriginalDefinition.ContainingNamespace?.ToDisplayString() + "." +
                           current.OriginalDefinition.MetadataName;
            if (fullName == metadataName)
                return true;
            current = current.BaseType;
        }
        return false;
    }
}
