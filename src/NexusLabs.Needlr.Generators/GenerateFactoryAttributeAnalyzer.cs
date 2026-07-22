using System.Collections.Immutable;
using System.Linq;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

using NexusLabs.Needlr.Roslyn.Shared;

namespace NexusLabs.Needlr.Generators;

/// <summary>
/// Analyzer that validates [GenerateFactory] and [GenerateFactory&lt;T&gt;] attribute usage:
/// - NDLRGEN003: All constructor parameters are injectable (factory unnecessary)
/// - NDLRGEN004: No constructor parameters are injectable (low value factory)
/// - NDLRGEN005: Type argument T is not an interface implemented by the class
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class GenerateFactoryAttributeAnalyzer : DiagnosticAnalyzer
{
    private const string GenerateFactoryAttributeName = "GenerateFactoryAttribute";
    private const string GeneratorsNamespace = "NexusLabs.Needlr.Generators";

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(
            DiagnosticDescriptors.FactoryAllParamsInjectable,
            DiagnosticDescriptors.FactoryNoInjectableParams,
            DiagnosticDescriptors.FactoryTypeArgNotImplemented);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterSyntaxNodeAction(AnalyzeAttribute, SyntaxKind.Attribute);
    }

    private static void AnalyzeAttribute(SyntaxNodeAnalysisContext context)
    {
        var attributeSyntax = (AttributeSyntax)context.Node;
        var attributeSymbol = context.SemanticModel.GetSymbolInfo(attributeSyntax).Symbol?.ContainingType;

        if (attributeSymbol == null)
            return;

        // Check if this is a [GenerateFactory] or [GenerateFactory<T>] attribute
        if (!IsGenerateFactoryAttribute(attributeSymbol))
            return;

        // Get the class this attribute is applied to
        var classDeclaration = attributeSyntax.Parent?.Parent as ClassDeclarationSyntax;
        if (classDeclaration == null)
            return;

        var classSymbol = context.SemanticModel.GetDeclaredSymbol(classDeclaration);
        if (classSymbol == null)
            return;

        // NDLRGEN005: Check if generic type argument is implemented by the class
        if (attributeSymbol.IsGenericType && attributeSymbol.TypeArguments.Length == 1)
        {
            var typeArg = attributeSymbol.TypeArguments[0] as INamedTypeSymbol;
            if (typeArg != null)
            {
                // Check if the class implements this interface
                bool implementsInterface = classSymbol.AllInterfaces.Any(i =>
                    SymbolEqualityComparer.Default.Equals(i, typeArg));

                if (!implementsInterface)
                {
                    var diagnostic = Diagnostic.Create(
                        DiagnosticDescriptors.FactoryTypeArgNotImplemented,
                        attributeSyntax.GetLocation(),
                        classSymbol.Name,
                        typeArg.Name);

                    context.ReportDiagnostic(diagnostic);
                }
            }
        }

        // Analyze constructor parameters for NDLRGEN003 and NDLRGEN004
        AnalyzeConstructorParameters(context, attributeSyntax, classSymbol);
    }

    private static void AnalyzeConstructorParameters(
        SyntaxNodeAnalysisContext context,
        AttributeSyntax attributeSyntax,
        INamedTypeSymbol classSymbol)
    {
        var parameterTypes = TryGetGeneratedConstructorParameterTypes(classSymbol) ?? GetBestExplicitConstructorParameterTypes(classSymbol);
        if (parameterTypes is null)
            return;

        if (parameterTypes.Count == 0)
        {
            // No parameters at all - factory is pointless
            var diagnostic = Diagnostic.Create(
                DiagnosticDescriptors.FactoryNoInjectableParams,
                attributeSyntax.GetLocation(),
                classSymbol.Name);

            context.ReportDiagnostic(diagnostic);
            return;
        }

        int injectableCount = 0;
        int runtimeCount = 0;

        foreach (var parameterType in parameterTypes)
        {
            if (IsInjectableParameterType(parameterType))
            {
                injectableCount++;
            }
            else
            {
                runtimeCount++;
            }
        }

        // NDLRGEN003: All params are injectable - factory unnecessary
        if (runtimeCount == 0)
        {
            var diagnostic = Diagnostic.Create(
                DiagnosticDescriptors.FactoryAllParamsInjectable,
                attributeSyntax.GetLocation(),
                classSymbol.Name);

            context.ReportDiagnostic(diagnostic);
        }
        // NDLRGEN004: No params are injectable - low value factory
        else if (injectableCount == 0)
        {
            var diagnostic = Diagnostic.Create(
                DiagnosticDescriptors.FactoryNoInjectableParams,
                attributeSyntax.GetLocation(),
                classSymbol.Name);

            context.ReportDiagnostic(diagnostic);
        }
    }

    /// <summary>
    /// Returns the effective constructor parameter types for a type eligible for
    /// generated-constructor generation (<c>[GenerateConstructor]</c> or a positive
    /// field-level constructor guard trigger), derived from the same shared eligible-field
    /// model the source generator and type-registry discovery use, or
    /// <see langword="null"/> when the type is not eligible. Such a type's effective
    /// constructor is emitted by a sibling generator pass rather than authored in its own
    /// syntax tree, so runtime-vs-injectable classification must use this field-derived
    /// parameter list instead of the type's (implicit, parameterless) symbol-visible
    /// constructor.
    /// </summary>
    private static IReadOnlyList<ITypeSymbol>? TryGetGeneratedConstructorParameterTypes(INamedTypeSymbol classSymbol)
    {
        if (!GeneratedConstructorEligibility.IsEligibleForGeneratedConstructor(classSymbol))
            return null;

        return GeneratedConstructorEligibility.GetEligibleConstructorFields(classSymbol)
            .Select(f => f.Type)
            .ToList();
    }

    /// <summary>
    /// Returns the parameter types of the best (public, most-parameters) explicitly
    /// authored instance constructor, or <see langword="null"/> when the type declares no
    /// public instance constructor at all.
    /// </summary>
    private static IReadOnlyList<ITypeSymbol>? GetBestExplicitConstructorParameterTypes(INamedTypeSymbol classSymbol)
    {
        var publicCtors = classSymbol.InstanceConstructors
            .Where(c => c.DeclaredAccessibility == Accessibility.Public && !c.IsStatic)
            .OrderByDescending(c => c.Parameters.Length)
            .ToList();

        if (publicCtors.Count == 0)
            return null;

        return publicCtors[0].Parameters.Select(p => p.Type).ToList();
    }

    private static bool IsGenerateFactoryAttribute(INamedTypeSymbol attributeSymbol)
    {
        // Handle both GenerateFactoryAttribute and GenerateFactoryAttribute<T>
        var name = attributeSymbol.Name;
        if (name != GenerateFactoryAttributeName)
        {
            // Check original definition for generic case
            if (attributeSymbol.IsGenericType)
            {
                name = attributeSymbol.OriginalDefinition.Name;
                if (name != GenerateFactoryAttributeName)
                    return false;
            }
            else
            {
                return false;
            }
        }

        var ns = attributeSymbol.ContainingNamespace?.ToString();
        return ns == GeneratorsNamespace;
    }

    private static bool IsInjectableParameterType(ITypeSymbol typeSymbol)
    {
        // Value types (int, string, DateTime, Guid, etc.) are NOT injectable
        if (typeSymbol.IsValueType)
            return false;

        // String is special - it's a reference type but not injectable
        if (typeSymbol.SpecialType == SpecialType.System_String)
            return false;

        // Delegates are not injectable
        if (typeSymbol.TypeKind == TypeKind.Delegate)
            return false;

        // Arrays are typically not injectable by DI
        if (typeSymbol.TypeKind == TypeKind.Array)
            return false;

        // Interfaces and classes are injectable
        if (typeSymbol.TypeKind == TypeKind.Interface || typeSymbol.TypeKind == TypeKind.Class)
            return true;

        return false;
    }
}
