using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace NexusLabs.Needlr.Analyzers;

/// <summary>
/// Analyzer that validates [GenerateFactory] and [GenerateFactory&lt;T&gt;] attribute usage:
/// - NDLRCOR012: All constructor parameters are injectable (factory unnecessary)
/// - NDLRCOR013: No constructor parameters are injectable (low value factory)
/// - NDLRCOR014: Type argument T is not an interface implemented by the class
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

        // NDLRCOR014: Check if generic type argument is implemented by the class
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

        // Analyze constructor parameters for NDLRCOR012 and NDLRCOR013
        AnalyzeConstructorParameters(context, attributeSyntax, classSymbol);
    }

    private static void AnalyzeConstructorParameters(
        SyntaxNodeAnalysisContext context,
        AttributeSyntax attributeSyntax,
        INamedTypeSymbol classSymbol)
    {
        // Find the best constructor (public, most parameters)
        var publicCtors = classSymbol.InstanceConstructors
            .Where(c => c.DeclaredAccessibility == Accessibility.Public && !c.IsStatic)
            .OrderByDescending(c => c.Parameters.Length)
            .ToList();

        if (publicCtors.Count == 0)
            return;

        // Check all constructors - we care about the "best" one for the warning
        var bestCtor = publicCtors[0];
        var parameters = bestCtor.Parameters;

        if (parameters.Length == 0)
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

        foreach (var param in parameters)
        {
            if (IsInjectableParameterType(param.Type))
            {
                injectableCount++;
            }
            else
            {
                runtimeCount++;
            }
        }

        // NDLRCOR012: All params are injectable - factory unnecessary
        if (runtimeCount == 0)
        {
            var diagnostic = Diagnostic.Create(
                DiagnosticDescriptors.FactoryAllParamsInjectable,
                attributeSyntax.GetLocation(),
                classSymbol.Name);

            context.ReportDiagnostic(diagnostic);
        }
        // NDLRCOR013: No params are injectable - low value factory
        else if (injectableCount == 0)
        {
            var diagnostic = Diagnostic.Create(
                DiagnosticDescriptors.FactoryNoInjectableParams,
                attributeSyntax.GetLocation(),
                classSymbol.Name);

            context.ReportDiagnostic(diagnostic);
        }
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
