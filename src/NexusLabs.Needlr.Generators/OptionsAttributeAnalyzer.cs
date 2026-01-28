// Copyright (c) NexusLabs. All rights reserved.
// Licensed under the MIT License.

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

        // Get the class this attribute is applied to
        var classDeclaration = attributeSyntax.Parent?.Parent as ClassDeclarationSyntax;
        if (classDeclaration == null)
            return;

        var classSymbol = context.SemanticModel.GetDeclaredSymbol(classDeclaration);
        if (classSymbol == null)
            return;

        // Extract attribute properties
        var attributeData = classSymbol.GetAttributes()
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
            var targetType = validatorType ?? classSymbol;
            var methodName = validateMethod ?? "Validate";

            // Find the validation method
            var validationMethod = FindValidationMethod(targetType, methodName);

            // NDLRGEN016: Method not found
            if (validationMethod == null)
            {
                // Only report if ValidateMethod was explicitly specified or Validator was specified
                // (convention-based discovery is optional - no method is OK if not specified)
                if (validateMethod != null || validatorType != null)
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        DiagnosticDescriptors.ValidateMethodNotFound,
                        attributeSyntax.GetLocation(),
                        methodName,
                        targetType.Name));
                }
            }
            else
            {
                // NDLRGEN017: Check method signature
                var signatureError = ValidateMethodSignature(validationMethod, classSymbol, validatorType != null);
                if (signatureError != null)
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        DiagnosticDescriptors.ValidateMethodWrongSignature,
                        attributeSyntax.GetLocation(),
                        methodName,
                        targetType.Name,
                        signatureError));
                }

                // NDLRGEN015: Validator type mismatch (if external validator with parameter)
                if (validatorType != null && !validationMethod.IsStatic && validationMethod.Parameters.Length == 1)
                {
                    var paramType = validationMethod.Parameters[0].Type;
                    if (!SymbolEqualityComparer.Default.Equals(paramType, classSymbol))
                    {
                        context.ReportDiagnostic(Diagnostic.Create(
                            DiagnosticDescriptors.ValidatorTypeMismatch,
                            attributeSyntax.GetLocation(),
                            validatorType.Name,
                            paramType.Name,
                            classSymbol.Name));
                    }
                }
            }

            // NDLRGEN014: Check if validator implements IOptionsValidator<T>
            if (validatorType != null && validationMethod == null)
            {
                var implementsInterface = ImplementsIOptionsValidator(validatorType, classSymbol);
                if (!implementsInterface)
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        DiagnosticDescriptors.ValidatorTypeMissingInterface,
                        attributeSyntax.GetLocation(),
                        validatorType.Name,
                        classSymbol.Name));
                }
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

    private static IMethodSymbol? FindValidationMethod(INamedTypeSymbol targetType, string methodName)
    {
        foreach (var member in targetType.GetMembers())
        {
            if (member is IMethodSymbol method && method.Name == methodName)
            {
                // Accept methods with 0 or 1 parameters
                if (method.Parameters.Length <= 1)
                    return method;
            }
        }

        return null;
    }

    private static string? ValidateMethodSignature(IMethodSymbol method, INamedTypeSymbol optionsType, bool isExternalValidator)
    {
        // Check return type - should be IEnumerable<something>
        if (method.ReturnType is not INamedTypeSymbol returnType)
            return "IEnumerable<ValidationError> or IEnumerable<string>";

        var isEnumerable = returnType.OriginalDefinition.ToDisplayString() == "System.Collections.Generic.IEnumerable<T>" ||
                           returnType.AllInterfaces.Any(i => i.OriginalDefinition.ToDisplayString() == "System.Collections.Generic.IEnumerable<T>");

        if (!isEnumerable && returnType.ToDisplayString() != "System.Collections.IEnumerable")
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
        }

        return null; // Valid signature
    }

    private static bool ImplementsIOptionsValidator(INamedTypeSymbol validatorType, INamedTypeSymbol optionsType)
    {
        foreach (var iface in validatorType.AllInterfaces)
        {
            if (iface.Name == IOptionsValidatorName &&
                iface.ContainingNamespace?.ToDisplayString() == GeneratorsNamespace &&
                iface.IsGenericType &&
                iface.TypeArguments.Length == 1)
            {
                // Check if the type argument matches the options type
                if (SymbolEqualityComparer.Default.Equals(iface.TypeArguments[0], optionsType))
                    return true;
            }
        }

        return false;
    }
}
