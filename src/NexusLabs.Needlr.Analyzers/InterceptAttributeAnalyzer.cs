using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace NexusLabs.Needlr.Analyzers;

/// <summary>
/// Analyzer that validates [Intercept] attribute usage:
/// - NDLRCOR007: Intercept type must implement IMethodInterceptor
/// - NDLRCOR008: [Intercept] applied to class without interfaces
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class InterceptAttributeAnalyzer : DiagnosticAnalyzer
{
    private const string InterceptAttributeName = "InterceptAttribute";
    private const string IMethodInterceptorName = "IMethodInterceptor";
    private const string NeedlrNamespace = "NexusLabs.Needlr";

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(
            DiagnosticDescriptors.InterceptTypeMustImplementInterface,
            DiagnosticDescriptors.InterceptOnClassWithoutInterfaces);

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

        // Check if this is an [Intercept] or [Intercept<T>] attribute
        if (!IsInterceptAttribute(attributeSymbol))
            return;

        // Get the interceptor type from the attribute
        var interceptorType = GetInterceptorType(attributeSyntax, attributeSymbol, context.SemanticModel);
        
        if (interceptorType != null)
        {
            // NDLRCOR007: Check if interceptor implements IMethodInterceptor
            if (!ImplementsIMethodInterceptor(interceptorType))
            {
                var diagnostic = Diagnostic.Create(
                    DiagnosticDescriptors.InterceptTypeMustImplementInterface,
                    attributeSyntax.GetLocation(),
                    interceptorType.Name);

                context.ReportDiagnostic(diagnostic);
            }
        }

        // Find the class/method this attribute is applied to
        var targetNode = attributeSyntax.Parent?.Parent;
        
        // If applied to a class, check NDLRCOR008
        if (targetNode is ClassDeclarationSyntax classDeclaration)
        {
            var classSymbol = context.SemanticModel.GetDeclaredSymbol(classDeclaration);
            if (classSymbol != null)
            {
                // Check if class implements any non-system interfaces
                var hasUserInterface = classSymbol.AllInterfaces.Any(i => 
                    !IsSystemInterface(i) && !IsNeedlrInternalInterface(i));

                if (!hasUserInterface)
                {
                    var diagnostic = Diagnostic.Create(
                        DiagnosticDescriptors.InterceptOnClassWithoutInterfaces,
                        attributeSyntax.GetLocation(),
                        classDeclaration.Identifier.Text);

                    context.ReportDiagnostic(diagnostic);
                }
            }
        }
    }

    private static bool IsInterceptAttribute(INamedTypeSymbol attributeSymbol)
    {
        // Check for InterceptAttribute or InterceptAttribute<T>
        var name = attributeSymbol.Name;
        if (name != InterceptAttributeName)
            return false;

        var ns = attributeSymbol.ContainingNamespace?.ToString();
        return ns == NeedlrNamespace;
    }

    private static INamedTypeSymbol? GetInterceptorType(
        AttributeSyntax attributeSyntax,
        INamedTypeSymbol attributeSymbol,
        SemanticModel semanticModel)
    {
        // Generic attribute: [Intercept<LoggingInterceptor>]
        if (attributeSymbol.IsGenericType && attributeSymbol.TypeArguments.Length == 1)
        {
            return attributeSymbol.TypeArguments[0] as INamedTypeSymbol;
        }

        // Non-generic attribute: [Intercept(typeof(LoggingInterceptor))]
        if (attributeSyntax.ArgumentList?.Arguments.Count > 0)
        {
            var firstArg = attributeSyntax.ArgumentList.Arguments[0].Expression;
            if (firstArg is TypeOfExpressionSyntax typeOfExpr)
            {
                var typeInfo = semanticModel.GetTypeInfo(typeOfExpr.Type);
                return typeInfo.Type as INamedTypeSymbol;
            }
        }

        return null;
    }

    private static bool ImplementsIMethodInterceptor(INamedTypeSymbol typeSymbol)
    {
        return typeSymbol.AllInterfaces.Any(i =>
            i.Name == IMethodInterceptorName &&
            i.ContainingNamespace?.ToString() == NeedlrNamespace);
    }

    private static bool IsSystemInterface(INamedTypeSymbol interfaceSymbol)
    {
        var ns = interfaceSymbol.ContainingNamespace?.ToString() ?? "";
        return ns.StartsWith("System", StringComparison.Ordinal) ||
               ns.StartsWith("Microsoft", StringComparison.Ordinal);
    }

    private static bool IsNeedlrInternalInterface(INamedTypeSymbol interfaceSymbol)
    {
        // IMethodInterceptor is an internal Needlr interface, not a user service interface
        return interfaceSymbol.Name == IMethodInterceptorName &&
               interfaceSymbol.ContainingNamespace?.ToString() == NeedlrNamespace;
    }
}
