using System.Collections.Concurrent;
using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace NexusLabs.Needlr.Analyzers;

/// <summary>
/// Analyzer that detects Lazy&lt;T&gt; dependencies where T is not discovered
/// by source generation. Only active when [assembly: GenerateTypeRegistry] is present.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class LazyResolutionAnalyzer : DiagnosticAnalyzer
{
    private const string GenerateTypeRegistryAttributeName = "NexusLabs.Needlr.Generators.GenerateTypeRegistryAttribute";

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(DiagnosticDescriptors.LazyResolutionUnknown);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(compilationContext =>
        {
            if (!HasGenerateTypeRegistryAttribute(compilationContext.Compilation))
                return;

            var lazyType = compilationContext.Compilation.GetTypeByMetadataName("System.Lazy`1");
            if (lazyType == null)
                return;

            // Collect all discovered types and interfaces during compilation
            var discoveredTypes = new ConcurrentDictionary<INamedTypeSymbol, byte>(SymbolEqualityComparer.Default);
            var discoveredInterfaces = new ConcurrentDictionary<INamedTypeSymbol, byte>(SymbolEqualityComparer.Default);

            // Collect pending diagnostics to verify at compilation end
            var pendingDiagnostics = new ConcurrentBag<(Location Location, INamedTypeSymbol InnerType)>();

            // First pass: collect all discoverable types and their interfaces
            compilationContext.RegisterSymbolAction(symbolContext =>
            {
                var typeSymbol = (INamedTypeSymbol)symbolContext.Symbol;

                if (!IsDiscoverableClass(typeSymbol))
                    return;

                discoveredTypes.TryAdd(typeSymbol, 0);
                foreach (var iface in typeSymbol.AllInterfaces)
                {
                    discoveredInterfaces.TryAdd(iface, 0);
                }
            }, SymbolKind.NamedType);

            // Second pass: collect Lazy<T> parameters for later verification
            compilationContext.RegisterSyntaxNodeAction(
                ctx => CollectLazyParameter(ctx, lazyType, pendingDiagnostics),
                SyntaxKind.Parameter);

            // At compilation end, verify and report diagnostics
            compilationContext.RegisterCompilationEndAction(endContext =>
            {
                foreach (var (location, innerType) in pendingDiagnostics)
                {
                    // Framework types are typically registered by the framework
                    var ns = innerType.ContainingNamespace?.ToDisplayString() ?? "";
                    if (ns.StartsWith("Microsoft.Extensions.") ||
                        ns.StartsWith("Microsoft.AspNetCore.") ||
                        ns == "System" ||
                        ns.StartsWith("System."))
                    {
                        continue;
                    }

                    // Check if the type or its interface is discovered
                    if (innerType.TypeKind == TypeKind.Interface)
                    {
                        if (discoveredInterfaces.ContainsKey(innerType))
                            continue;
                    }
                    else
                    {
                        if (discoveredTypes.ContainsKey(innerType))
                            continue;
                    }

                    var diagnostic = Diagnostic.Create(
                        DiagnosticDescriptors.LazyResolutionUnknown,
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

    private static void CollectLazyParameter(
        SyntaxNodeAnalysisContext context,
        INamedTypeSymbol lazyType,
        ConcurrentBag<(Location, INamedTypeSymbol)> pendingDiagnostics)
    {
        var parameter = (ParameterSyntax)context.Node;
        if (parameter.Type == null)
            return;

        var typeInfo = context.SemanticModel.GetTypeInfo(parameter.Type);
        if (typeInfo.Type is not INamedTypeSymbol parameterType)
            return;

        if (!SymbolEqualityComparer.Default.Equals(parameterType.OriginalDefinition, lazyType))
            return;

        if (parameterType.TypeArguments.Length != 1)
            return;

        var innerType = parameterType.TypeArguments[0] as INamedTypeSymbol;
        if (innerType == null)
            return;

        pendingDiagnostics.Add((parameter.Type.GetLocation(), innerType));
    }

    private static bool IsDiscoverableClass(INamedTypeSymbol typeSymbol)
    {
        // Skip non-class types
        if (typeSymbol.TypeKind != TypeKind.Class)
            return false;

        // Skip abstract classes
        if (typeSymbol.IsAbstract)
            return false;

        // Check for exclusion attributes
        foreach (var attr in typeSymbol.GetAttributes())
        {
            var name = attr.AttributeClass?.Name;
            if (name is "DoNotInjectAttribute" or "DoNotInject" or
                        "DoNotAutoRegisterAttribute" or "DoNotAutoRegister")
            {
                return false;
            }
        }

        // Check for explicit registration attributes
        foreach (var attr in typeSymbol.GetAttributes())
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
        return typeSymbol.AllInterfaces.Length > 0;
    }
}
