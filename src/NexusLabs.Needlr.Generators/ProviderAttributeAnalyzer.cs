using System.Collections.Immutable;
using System.Linq;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace NexusLabs.Needlr.Generators;

/// <summary>
/// Analyzer that validates [Provider] attribute usage:
/// - NDLRGEN031: [Provider] on class requires `partial` modifier
/// - NDLRGEN032: [Provider] interface must only contain get-only properties
/// - NDLRGEN033: Provider property type is a concrete class
/// - NDLRGEN034: Circular provider dependency detected
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ProviderAttributeAnalyzer : DiagnosticAnalyzer
{
    private const string ProviderAttributeName = "ProviderAttribute";
    private const string GeneratorsNamespace = "NexusLabs.Needlr.Generators";

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(
            DiagnosticDescriptors.ProviderClassNotPartial,
            DiagnosticDescriptors.ProviderInterfaceInvalidMember,
            DiagnosticDescriptors.ProviderPropertyConcreteType,
            DiagnosticDescriptors.ProviderCircularDependency);

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

        if (!IsProviderAttribute(attributeSymbol))
            return;

        var parent = attributeSyntax.Parent?.Parent;

        if (parent is ClassDeclarationSyntax classDeclaration)
        {
            AnalyzeProviderClass(context, classDeclaration, attributeSyntax);
        }
        else if (parent is InterfaceDeclarationSyntax interfaceDeclaration)
        {
            AnalyzeProviderInterface(context, interfaceDeclaration, attributeSyntax);
        }
    }

    private static void AnalyzeProviderClass(
        SyntaxNodeAnalysisContext context,
        ClassDeclarationSyntax classDeclaration,
        AttributeSyntax attributeSyntax)
    {
        // NDLRGEN031: Check for partial modifier
        var isPartial = classDeclaration.Modifiers.Any(m => m.IsKind(SyntaxKind.PartialKeyword));
        if (!isPartial)
        {
            var classSymbol = context.SemanticModel.GetDeclaredSymbol(classDeclaration);
            if (classSymbol != null)
            {
                context.ReportDiagnostic(
                    Diagnostic.Create(
                        DiagnosticDescriptors.ProviderClassNotPartial,
                        attributeSyntax.GetLocation(),
                        classSymbol.Name));
            }
        }
    }

    private static void AnalyzeProviderInterface(
        SyntaxNodeAnalysisContext context,
        InterfaceDeclarationSyntax interfaceDeclaration,
        AttributeSyntax attributeSyntax)
    {
        var interfaceSymbol = context.SemanticModel.GetDeclaredSymbol(interfaceDeclaration);
        if (interfaceSymbol == null)
            return;

        foreach (var member in interfaceSymbol.GetMembers())
        {
            // Skip properties - they're valid
            if (member is IPropertySymbol propertySymbol)
            {
                // NDLRGEN032: Check property is get-only
                if (propertySymbol.SetMethod != null)
                {
                    context.ReportDiagnostic(
                        Diagnostic.Create(
                            DiagnosticDescriptors.ProviderInterfaceInvalidMember,
                            GetMemberLocation(interfaceDeclaration, member.Name) ?? attributeSyntax.GetLocation(),
                            interfaceSymbol.Name,
                            $"a settable property '{member.Name}'"));
                }
                else
                {
                    // NDLRGEN033: Check for concrete type (warning only)
                    AnalyzePropertyType(context, interfaceSymbol, propertySymbol, interfaceDeclaration);
                }
                continue;
            }

            // Skip special members (constructors, etc.)
            if (member.IsImplicitlyDeclared)
                continue;

            // NDLRGEN032: Report invalid member types
            var memberDescription = member switch
            {
                IMethodSymbol m when m.MethodKind == MethodKind.Ordinary => $"a method '{member.Name}'",
                IEventSymbol => $"an event '{member.Name}'",
                _ => $"an unsupported member '{member.Name}'"
            };

            if (member is IMethodSymbol methodSymbol && methodSymbol.MethodKind != MethodKind.Ordinary)
                continue; // Skip property getters/setters

            context.ReportDiagnostic(
                Diagnostic.Create(
                    DiagnosticDescriptors.ProviderInterfaceInvalidMember,
                    GetMemberLocation(interfaceDeclaration, member.Name) ?? attributeSyntax.GetLocation(),
                    interfaceSymbol.Name,
                    memberDescription));
        }
    }

    private static void AnalyzePropertyType(
        SyntaxNodeAnalysisContext context,
        INamedTypeSymbol interfaceSymbol,
        IPropertySymbol propertySymbol,
        InterfaceDeclarationSyntax interfaceDeclaration)
    {
        var propertyType = propertySymbol.Type;

        // Unwrap nullable
        if (propertyType is INamedTypeSymbol namedType && namedType.IsGenericType)
        {
            var definition = namedType.OriginalDefinition.ToDisplayString();
            if (definition == "System.Nullable<T>")
            {
                propertyType = namedType.TypeArguments[0];
            }
            // Skip collections - they're always fine
            if (definition.StartsWith("System.Collections.Generic.IEnumerable<") ||
                definition.StartsWith("System.Collections.Generic.IReadOnlyCollection<") ||
                definition.StartsWith("System.Collections.Generic.IReadOnlyList<"))
            {
                return;
            }
        }

        // Skip interfaces - they're the recommended pattern
        if (propertyType.TypeKind == TypeKind.Interface)
            return;

        // Skip factory types (they end with Factory)
        if (propertyType.Name.EndsWith("Factory"))
            return;

        // Skip provider types (nested providers are fine)
        if (HasProviderAttribute(propertyType))
            return;

        // NDLRGEN033: Concrete class type detected
        if (propertyType.TypeKind == TypeKind.Class)
        {
            context.ReportDiagnostic(
                Diagnostic.Create(
                    DiagnosticDescriptors.ProviderPropertyConcreteType,
                    GetMemberLocation(interfaceDeclaration, propertySymbol.Name) ?? Location.None,
                    interfaceSymbol.Name,
                    propertySymbol.Name,
                    propertyType.ToDisplayString()));
        }
    }

    private static bool HasProviderAttribute(ITypeSymbol type)
    {
        return type.GetAttributes().Any(a =>
            a.AttributeClass?.Name == ProviderAttributeName &&
            a.AttributeClass.ContainingNamespace?.ToDisplayString() == GeneratorsNamespace);
    }

    private static Location? GetMemberLocation(TypeDeclarationSyntax typeDeclaration, string memberName)
    {
        foreach (var member in typeDeclaration.Members)
        {
            if (member is PropertyDeclarationSyntax prop && prop.Identifier.Text == memberName)
                return prop.GetLocation();
            if (member is MethodDeclarationSyntax method && method.Identifier.Text == memberName)
                return method.GetLocation();
            if (member is EventDeclarationSyntax evt && evt.Identifier.Text == memberName)
                return evt.GetLocation();
        }
        return null;
    }

    private static bool IsProviderAttribute(INamedTypeSymbol attributeSymbol)
    {
        if (attributeSymbol.Name != ProviderAttributeName)
            return false;

        return attributeSymbol.ContainingNamespace?.ToDisplayString() == GeneratorsNamespace;
    }
}
