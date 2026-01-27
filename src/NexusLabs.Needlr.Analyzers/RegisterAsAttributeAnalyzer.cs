using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace NexusLabs.Needlr.Analyzers;

/// <summary>
/// Analyzer that validates [RegisterAs&lt;T&gt;] attribute usage:
/// - NDLRCOR015: Type argument T is not an interface implemented by the class
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class RegisterAsAttributeAnalyzer : DiagnosticAnalyzer
{
    private const string RegisterAsAttributeName = "RegisterAsAttribute";
    private const string NeedlrNamespace = "NexusLabs.Needlr";

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(DiagnosticDescriptors.RegisterAsTypeArgNotImplemented);

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

        // Check if this is a [RegisterAs<T>] attribute
        if (!IsRegisterAsAttribute(attributeSymbol))
            return;

        // Get the class this attribute is applied to
        var classDeclaration = attributeSyntax.Parent?.Parent as ClassDeclarationSyntax;
        if (classDeclaration == null)
            return;

        var classSymbol = context.SemanticModel.GetDeclaredSymbol(classDeclaration);
        if (classSymbol == null)
            return;

        // NDLRCOR015: Check if generic type argument is implemented by the class
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
                        DiagnosticDescriptors.RegisterAsTypeArgNotImplemented,
                        attributeSyntax.GetLocation(),
                        classSymbol.Name,
                        typeArg.Name);

                    context.ReportDiagnostic(diagnostic);
                }
            }
        }
    }

    private static bool IsRegisterAsAttribute(INamedTypeSymbol attributeSymbol)
    {
        // Handle RegisterAsAttribute<T>
        var name = attributeSymbol.Name;
        if (name != RegisterAsAttributeName)
        {
            // Check original definition for generic case
            if (attributeSymbol.IsGenericType)
            {
                name = attributeSymbol.OriginalDefinition.Name;
                if (name != RegisterAsAttributeName)
                    return false;
            }
            else
            {
                return false;
            }
        }

        var ns = attributeSymbol.ContainingNamespace?.ToString();
        return ns == NeedlrNamespace;
    }
}
