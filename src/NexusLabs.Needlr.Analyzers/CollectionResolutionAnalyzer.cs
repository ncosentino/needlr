using System.Collections.Concurrent;
using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace NexusLabs.Needlr.Analyzers;

/// <summary>
/// Analyzer that detects IEnumerable&lt;T&gt; dependencies where no implementations of T
/// are discovered by source generation. Only active when [assembly: GenerateTypeRegistry] is present.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class CollectionResolutionAnalyzer : DiagnosticAnalyzer
{
    private const string GenerateTypeRegistryAttributeName = "NexusLabs.Needlr.Generators.GenerateTypeRegistryAttribute";

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(DiagnosticDescriptors.CollectionResolutionEmpty);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(compilationContext =>
        {
            if (!HasGenerateTypeRegistryAttribute(compilationContext.Compilation))
                return;

            var enumerableType = compilationContext.Compilation.GetTypeByMetadataName("System.Collections.Generic.IEnumerable`1");
            if (enumerableType == null)
                return;

            // Collect all discovered interface implementations during compilation
            var discoveredInterfaces = new ConcurrentDictionary<INamedTypeSymbol, byte>(SymbolEqualityComparer.Default);

            // Collect pending diagnostics to verify at compilation end
            var pendingDiagnostics = new ConcurrentBag<(Location Location, INamedTypeSymbol InnerType)>();

            // First pass: collect all interfaces that have implementations
            compilationContext.RegisterSymbolAction(symbolContext =>
            {
                var typeSymbol = (INamedTypeSymbol)symbolContext.Symbol;

                if (!IsDiscoverableClass(typeSymbol))
                    return;

                foreach (var iface in typeSymbol.AllInterfaces)
                {
                    discoveredInterfaces.TryAdd(iface, 0);
                }
            }, SymbolKind.NamedType);

            // Second pass: collect IEnumerable<T> parameters for later verification
            compilationContext.RegisterSyntaxNodeAction(
                ctx => CollectEnumerableParameter(ctx, enumerableType, pendingDiagnostics),
                SyntaxKind.Parameter);

            // At compilation end, verify and report diagnostics
            compilationContext.RegisterCompilationEndAction(endContext =>
            {
                foreach (var (location, innerType) in pendingDiagnostics)
                {
                    // Framework interfaces are typically populated by the framework
                    var ns = innerType.ContainingNamespace?.ToDisplayString() ?? "";
                    if (ns.StartsWith("Microsoft.Extensions.") ||
                        ns.StartsWith("Microsoft.AspNetCore.") ||
                        ns == "System" ||
                        ns.StartsWith("System."))
                    {
                        continue;
                    }

                    // Check if we discovered any implementations
                    if (discoveredInterfaces.ContainsKey(innerType))
                        continue;

                    var diagnostic = Diagnostic.Create(
                        DiagnosticDescriptors.CollectionResolutionEmpty,
                        location,
                        innerType.Name);

                    endContext.ReportDiagnostic(diagnostic);
                }
            });
        });
    }

    private static bool HasGenerateTypeRegistryAttribute(Compilation compilation)
    {
        foreach (var attribute in compilation.Assembly.GetAttributes())
        {
            var fullName = attribute.AttributeClass?.ToDisplayString();
            if (fullName == GenerateTypeRegistryAttributeName)
                return true;
        }

        return false;
    }

    private static void CollectEnumerableParameter(
        SyntaxNodeAnalysisContext context,
        INamedTypeSymbol enumerableType,
        ConcurrentBag<(Location, INamedTypeSymbol)> pendingDiagnostics)
    {
        var parameter = (ParameterSyntax)context.Node;
        if (parameter.Type == null)
            return;

        var typeInfo = context.SemanticModel.GetTypeInfo(parameter.Type);
        if (typeInfo.Type is not INamedTypeSymbol parameterType)
            return;

        if (!SymbolEqualityComparer.Default.Equals(parameterType.OriginalDefinition, enumerableType))
            return;

        if (parameterType.TypeArguments.Length != 1)
            return;

        var innerType = parameterType.TypeArguments[0] as INamedTypeSymbol;
        if (innerType == null)
            return;

        // Only warn for interface types - concrete types in IEnumerable<T> are unusual
        if (innerType.TypeKind != TypeKind.Interface)
            return;

        pendingDiagnostics.Add((parameter.Type.GetLocation(), innerType));
    }

    private static bool IsDiscoverableClass(INamedTypeSymbol classSymbol)
    {
        // Skip non-class types
        if (classSymbol.TypeKind != TypeKind.Class)
            return false;

        // Skip abstract classes
        if (classSymbol.IsAbstract)
            return false;

        // Check for exclusion attributes
        foreach (var attr in classSymbol.GetAttributes())
        {
            var name = attr.AttributeClass?.Name;
            if (name is "DoNotInjectAttribute" or "DoNotInject" or
                        "DoNotAutoRegisterAttribute" or "DoNotAutoRegister")
            {
                return false;
            }
        }

        // Check for explicit registration attributes
        foreach (var attr in classSymbol.GetAttributes())
        {
            var name = attr.AttributeClass?.Name;
            if (name is "SingletonAttribute" or "Singleton" or
                        "ScopedAttribute" or "Scoped" or
                        "TransientAttribute" or "Transient" or
                        "RegisterAsAttribute" or "RegisterAs" or
                        "AutoRegisterAttribute" or "AutoRegister")
            {
                return true;
            }
        }

        // Classes with interfaces are auto-discovered by Needlr
        return classSymbol.AllInterfaces.Length > 0;
    }
}
