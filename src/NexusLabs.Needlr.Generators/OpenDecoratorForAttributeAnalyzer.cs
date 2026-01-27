using System.Collections.Immutable;
using System.Linq;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace NexusLabs.Needlr.Generators;

/// <summary>
/// Analyzer that validates [OpenDecoratorFor] attribute usage:
/// - NDLRGEN006: Type argument must be an open generic interface
/// - NDLRGEN007: Decorator class must be an open generic with matching arity
/// - NDLRGEN008: Decorator class must implement the open generic interface
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class OpenDecoratorForAttributeAnalyzer : DiagnosticAnalyzer
{
    private const string OpenDecoratorForAttributeName = "OpenDecoratorForAttribute";
    private const string GeneratorsNamespace = "NexusLabs.Needlr.Generators";

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(
            DiagnosticDescriptors.OpenDecoratorTypeNotOpenGeneric,
            DiagnosticDescriptors.OpenDecoratorClassNotOpenGeneric,
            DiagnosticDescriptors.OpenDecoratorNotImplementingInterface);

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

        // Check if this is an [OpenDecoratorFor] attribute
        if (!IsOpenDecoratorForAttribute(attributeSymbol))
            return;

        // Get the class this attribute is applied to
        var classDeclaration = attributeSyntax.Parent?.Parent as ClassDeclarationSyntax;
        if (classDeclaration == null)
            return;

        var classSymbol = context.SemanticModel.GetDeclaredSymbol(classDeclaration);
        if (classSymbol == null)
            return;

        // Get the type argument from the attribute constructor
        var typeArg = GetTypeArgumentFromAttribute(attributeSyntax, context.SemanticModel);
        if (typeArg == null)
            return;

        // NDLRGEN006: Validate type argument is an open generic interface
        if (!IsOpenGenericInterface(typeArg, out string typeDescription))
        {
            var diagnostic = Diagnostic.Create(
                DiagnosticDescriptors.OpenDecoratorTypeNotOpenGeneric,
                attributeSyntax.GetLocation(),
                typeArg.ToDisplayString(),
                typeDescription);

            context.ReportDiagnostic(diagnostic);
            return; // Don't check other rules if this fails
        }

        var openGenericInterface = typeArg as INamedTypeSymbol;
        if (openGenericInterface == null)
            return;

        int expectedTypeParamCount = openGenericInterface.TypeParameters.Length;

        // NDLRGEN007: Validate decorator class is an open generic with matching arity
        if (!classSymbol.IsGenericType || classSymbol.TypeParameters.Length != expectedTypeParamCount)
        {
            var diagnostic = Diagnostic.Create(
                DiagnosticDescriptors.OpenDecoratorClassNotOpenGeneric,
                attributeSyntax.GetLocation(),
                classSymbol.Name,
                openGenericInterface.ToDisplayString(),
                expectedTypeParamCount);

            context.ReportDiagnostic(diagnostic);
            return; // Don't check implementation if arity doesn't match
        }

        // NDLRGEN008: Validate decorator implements the open generic interface
        if (!ImplementsOpenGenericInterface(classSymbol, openGenericInterface))
        {
            var diagnostic = Diagnostic.Create(
                DiagnosticDescriptors.OpenDecoratorNotImplementingInterface,
                attributeSyntax.GetLocation(),
                classSymbol.Name,
                openGenericInterface.ToDisplayString());

            context.ReportDiagnostic(diagnostic);
        }
    }

    private static bool IsOpenDecoratorForAttribute(INamedTypeSymbol attributeSymbol)
    {
        if (attributeSymbol.Name != OpenDecoratorForAttributeName)
            return false;

        var ns = attributeSymbol.ContainingNamespace?.ToString();
        return ns == GeneratorsNamespace;
    }

    private static ITypeSymbol? GetTypeArgumentFromAttribute(
        AttributeSyntax attributeSyntax,
        SemanticModel semanticModel)
    {
        // The attribute has a constructor: OpenDecoratorForAttribute(Type openGenericServiceType)
        // We need to get the typeof() argument
        var argumentList = attributeSyntax.ArgumentList;
        if (argumentList == null || argumentList.Arguments.Count == 0)
            return null;

        var firstArg = argumentList.Arguments[0];
        var expression = firstArg.Expression;

        // Handle typeof(IHandler<>)
        if (expression is TypeOfExpressionSyntax typeOfExpr)
        {
            var typeInfo = semanticModel.GetTypeInfo(typeOfExpr.Type);
            return typeInfo.Type;
        }

        return null;
    }

    private static bool IsOpenGenericInterface(ITypeSymbol? typeSymbol, out string typeDescription)
    {
        if (typeSymbol == null)
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

        // Check if it's an unbound/open generic (has unsubstituted type parameters)
        if (!namedType.IsUnboundGenericType && 
            !namedType.TypeArguments.All(t => t.TypeKind == TypeKind.TypeParameter))
        {
            typeDescription = "closed generic interface (use typeof(IInterface<>) not typeof(IInterface<T>))";
            return false;
        }

        typeDescription = "open generic interface";
        return true;
    }

    private static bool ImplementsOpenGenericInterface(
        INamedTypeSymbol classSymbol,
        INamedTypeSymbol openGenericInterface)
    {
        // Check if the class implements a constructed version of the open generic interface
        // e.g., LoggingDecorator<T> : IHandler<T> should match IHandler<>
        var unboundInterface = openGenericInterface.IsUnboundGenericType 
            ? openGenericInterface 
            : openGenericInterface.ConstructUnboundGenericType();

        foreach (var iface in classSymbol.AllInterfaces)
        {
            if (!iface.IsGenericType)
                continue;

            var unboundImplemented = iface.ConstructUnboundGenericType();
            if (SymbolEqualityComparer.Default.Equals(unboundImplemented, unboundInterface))
                return true;
        }

        return false;
    }
}
