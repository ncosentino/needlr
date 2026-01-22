using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace NexusLabs.Needlr.Analyzers;

/// <summary>
/// Analyzer that detects injectable types in the global namespace that won't be
/// discovered when IncludeNamespacePrefixes is set without an empty string.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class GlobalNamespaceTypeAnalyzer : DiagnosticAnalyzer
{
    private const string GenerateTypeRegistryAttributeName = "NexusLabs.Needlr.Generators.GenerateTypeRegistryAttribute";
    private const string SingletonAttributeName = "NexusLabs.Needlr.SingletonAttribute";
    private const string ScopedAttributeName = "NexusLabs.Needlr.ScopedAttribute";
    private const string TransientAttributeName = "NexusLabs.Needlr.TransientAttribute";
    private const string DoNotInjectAttributeName = "NexusLabs.Needlr.DoNotInjectAttribute";
    private const string DoNotAutoRegisterAttributeName = "NexusLabs.Needlr.DoNotAutoRegisterAttribute";

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(DiagnosticDescriptors.GlobalNamespaceTypeNotDiscovered);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(compilationContext =>
        {
            // Check if the assembly has [GenerateTypeRegistry] with IncludeNamespacePrefixes set
            var (hasAttribute, prefixes, includesEmptyString) = GetGenerateTypeRegistryInfo(compilationContext.Compilation.Assembly);

            if (!hasAttribute)
                return;

            // If no prefixes are set, all types are included (no warning needed)
            if (prefixes == null || prefixes.Length == 0)
                return;

            // If empty string is in prefixes, global namespace types are included
            if (includesEmptyString)
                return;

            // Register to check all named types
            compilationContext.RegisterSymbolAction(symbolContext =>
            {
                var typeSymbol = (INamedTypeSymbol)symbolContext.Symbol;

                // Only check types in the global namespace
                if (typeSymbol.ContainingNamespace?.IsGlobalNamespace != true)
                    return;

                // Skip types that are excluded from injection
                if (HasAttribute(typeSymbol, DoNotInjectAttributeName) ||
                    HasAttribute(typeSymbol, DoNotAutoRegisterAttributeName))
                    return;

                // Check if this type looks injectable
                if (!IsLikelyInjectableType(typeSymbol))
                    return;

                // Report diagnostic
                var diagnostic = Diagnostic.Create(
                    DiagnosticDescriptors.GlobalNamespaceTypeNotDiscovered,
                    typeSymbol.Locations.FirstOrDefault(),
                    typeSymbol.Name);

                symbolContext.ReportDiagnostic(diagnostic);

            }, SymbolKind.NamedType);
        });
    }

    private static (bool hasAttribute, string[]? prefixes, bool includesEmptyString) GetGenerateTypeRegistryInfo(IAssemblySymbol assembly)
    {
        foreach (var attribute in assembly.GetAttributes())
        {
            var attrClass = attribute.AttributeClass;
            if (attrClass?.ToDisplayString() != GenerateTypeRegistryAttributeName)
                continue;

            string[]? prefixes = null;
            var includesEmptyString = false;

            foreach (var namedArg in attribute.NamedArguments)
            {
                if (namedArg.Key == "IncludeNamespacePrefixes" &&
                    !namedArg.Value.IsNull &&
                    namedArg.Value.Values.Length > 0)
                {
                    prefixes = namedArg.Value.Values
                        .Where(v => v.Value is string)
                        .Select(v => (string)v.Value!)
                        .ToArray();

                    includesEmptyString = prefixes.Any(p => string.IsNullOrEmpty(p));
                }
            }

            return (true, prefixes, includesEmptyString);
        }

        return (false, null, false);
    }

    private static bool HasAttribute(INamedTypeSymbol typeSymbol, string attributeName)
    {
        foreach (var attribute in typeSymbol.GetAttributes())
        {
            if (attribute.AttributeClass?.ToDisplayString() == attributeName)
                return true;
        }
        return false;
    }

    private static bool IsLikelyInjectableType(INamedTypeSymbol typeSymbol)
    {
        // Skip abstract types, interfaces, and static classes
        if (typeSymbol.IsAbstract || typeSymbol.TypeKind == TypeKind.Interface || typeSymbol.IsStatic)
            return false;

        // Skip generic type definitions (open generics)
        if (typeSymbol.IsGenericType && typeSymbol.TypeParameters.Length > 0 && typeSymbol.TypeArguments.Length == 0)
            return false;

        // Check if it has lifetime attributes
        if (HasAttribute(typeSymbol, SingletonAttributeName) ||
            HasAttribute(typeSymbol, ScopedAttributeName) ||
            HasAttribute(typeSymbol, TransientAttributeName))
            return true;

        // Check if it implements any interfaces (common for DI services)
        if (typeSymbol.AllInterfaces.Length > 0)
            return true;

        // Check if it has a constructor with interface/class parameters (likely DI dependencies)
        foreach (var constructor in typeSymbol.Constructors)
        {
            if (constructor.IsStatic)
                continue;

            foreach (var parameter in constructor.Parameters)
            {
                var paramType = parameter.Type;
                if (paramType.TypeKind == TypeKind.Interface ||
                    (paramType.TypeKind == TypeKind.Class && paramType.SpecialType == SpecialType.None))
                {
                    return true;
                }
            }
        }

        return false;
    }
}
