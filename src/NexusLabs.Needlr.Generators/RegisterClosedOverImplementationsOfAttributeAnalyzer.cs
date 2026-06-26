using System.Collections.Immutable;
using System.Linq;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace NexusLabs.Needlr.Generators;

/// <summary>
/// Analyzer that validates [RegisterClosedOverImplementationsOf] attribute usage:
/// - NDLRGEN035: Source type argument must be an open generic interface
/// - NDLRGEN036: Composition class must be an open generic with matching arity
/// - NDLRGEN037: Composition class must specify and implement the As service type
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class RegisterClosedOverImplementationsOfAttributeAnalyzer : DiagnosticAnalyzer
{
    private const string AttributeName = "RegisterClosedOverImplementationsOfAttribute";
    private const string GeneratorsNamespace = "NexusLabs.Needlr.Generators";

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(
            DiagnosticDescriptors.ComposedSourceNotOpenGenericInterface,
            DiagnosticDescriptors.ComposedClassNotOpenGeneric,
            DiagnosticDescriptors.ComposedClassNotImplementingAs);

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

        if (attributeSymbol is null)
            return;

        if (!IsComposedAttribute(attributeSymbol))
            return;

        if (attributeSyntax.Parent?.Parent is not ClassDeclarationSyntax classDeclaration)
            return;

        if (context.SemanticModel.GetDeclaredSymbol(classDeclaration) is not INamedTypeSymbol classSymbol)
            return;

        var sourceType = GetSourceTypeArgument(attributeSyntax, context.SemanticModel);
        if (sourceType is null)
            return;

        // NDLRGEN035: source must be an open generic interface.
        if (!IsOpenGenericInterface(sourceType, out var typeDescription))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.ComposedSourceNotOpenGenericInterface,
                attributeSyntax.GetLocation(),
                sourceType.ToDisplayString(),
                typeDescription));
            return;
        }

        if (sourceType is not INamedTypeSymbol sourceInterface)
            return;

        var expectedArity = sourceInterface.TypeParameters.Length;

        // NDLRGEN036: composition must be an open generic with matching arity.
        if (!classSymbol.IsGenericType || classSymbol.TypeParameters.Length != expectedArity)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.ComposedClassNotOpenGeneric,
                attributeSyntax.GetLocation(),
                classSymbol.Name,
                sourceInterface.ToDisplayString(),
                expectedArity));
            return;
        }

        // NDLRGEN037: composition must specify and implement the As service type.
        var asType = GetAsServiceType(attributeSyntax, context.SemanticModel);
        if (asType is null || !ImplementsServiceType(classSymbol, asType))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.ComposedClassNotImplementingAs,
                attributeSyntax.GetLocation(),
                classSymbol.Name));
        }
    }

    private static bool IsComposedAttribute(INamedTypeSymbol attributeSymbol) =>
        attributeSymbol.Name == AttributeName &&
        attributeSymbol.ContainingNamespace?.ToString() == GeneratorsNamespace;

    private static ITypeSymbol? GetSourceTypeArgument(AttributeSyntax attributeSyntax, SemanticModel semanticModel)
    {
        var argumentList = attributeSyntax.ArgumentList;
        if (argumentList is null || argumentList.Arguments.Count == 0)
            return null;

        // The first positional argument is the source open generic interface.
        var firstArgument = argumentList.Arguments.FirstOrDefault(a => a.NameEquals is null);
        if (firstArgument?.Expression is TypeOfExpressionSyntax typeOfExpression)
            return semanticModel.GetTypeInfo(typeOfExpression.Type).Type;

        return null;
    }

    private static ITypeSymbol? GetAsServiceType(AttributeSyntax attributeSyntax, SemanticModel semanticModel)
    {
        var argumentList = attributeSyntax.ArgumentList;
        if (argumentList is null)
            return null;

        var asArgument = argumentList.Arguments
            .FirstOrDefault(a => a.NameEquals?.Name.Identifier.Text == "As");

        if (asArgument?.Expression is TypeOfExpressionSyntax typeOfExpression)
            return semanticModel.GetTypeInfo(typeOfExpression.Type).Type;

        return null;
    }

    private static bool IsOpenGenericInterface(ITypeSymbol? typeSymbol, out string typeDescription)
    {
        if (typeSymbol is null)
        {
            typeDescription = "null";
            return false;
        }

        if (typeSymbol is not INamedTypeSymbol namedType)
        {
            typeDescription = $"{typeSymbol.TypeKind}";
            return false;
        }

        if (namedType.TypeKind != TypeKind.Interface)
        {
            typeDescription = $"{namedType.TypeKind} (not an interface)";
            return false;
        }

        if (!namedType.IsGenericType)
        {
            typeDescription = "non-generic interface";
            return false;
        }

        if (!namedType.IsUnboundGenericType &&
            !namedType.TypeArguments.All(t => t.TypeKind == TypeKind.TypeParameter))
        {
            typeDescription = "closed generic interface (use typeof(IInterface<>) not typeof(IInterface<T>))";
            return false;
        }

        typeDescription = "open generic interface";
        return true;
    }

    private static bool ImplementsServiceType(INamedTypeSymbol classSymbol, ITypeSymbol serviceType)
    {
        if (serviceType.TypeKind == TypeKind.Interface)
            return classSymbol.AllInterfaces.Any(i => SymbolEqualityComparer.Default.Equals(i, serviceType));

        for (var baseType = classSymbol.BaseType; baseType is not null; baseType = baseType.BaseType)
        {
            if (SymbolEqualityComparer.Default.Equals(baseType, serviceType))
                return true;
        }

        return false;
    }
}
