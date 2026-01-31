using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace NexusLabs.Needlr.Analyzers;

/// <summary>
/// Analyzer that detects when a longer-lived service holds a reference to a shorter-lived IDisposable.
/// This is a more severe form of captive dependency because the disposed object will still be referenced.
/// </summary>
/// <remarks>
/// This analyzer is conservative to avoid false positives:
/// - Only fires when both consumer and dependency have explicit lifetime attributes
/// - Only fires when the dependency type itself (not just interface) implements IDisposable/IAsyncDisposable
/// - Does not fire for factory patterns (Func&lt;T&gt;, Lazy&lt;T&gt;, IServiceScopeFactory)
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class DisposableCaptiveDependencyAnalyzer : DiagnosticAnalyzer
{
    private enum LifetimeRank
    {
        Unknown = -1,
        Transient = 0,
        Scoped = 1,
        Singleton = 2
    }

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(DiagnosticDescriptors.DisposableCaptiveDependency);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterSyntaxNodeAction(AnalyzeClassDeclaration, SyntaxKind.ClassDeclaration);
    }

    private static void AnalyzeClassDeclaration(SyntaxNodeAnalysisContext context)
    {
        var classDeclaration = (ClassDeclarationSyntax)context.Node;

        // Skip abstract classes - they can't be instantiated directly
        if (classDeclaration.Modifiers.Any(SyntaxKind.AbstractKeyword))
        {
            return;
        }

        var classSymbol = context.SemanticModel.GetDeclaredSymbol(classDeclaration);
        if (classSymbol == null)
        {
            return;
        }

        // Only analyze types with EXPLICIT lifetime attributes to avoid false positives
        var consumerLifetime = GetExplicitLifetimeFromAttributes(classSymbol);
        if (consumerLifetime == LifetimeRank.Unknown)
        {
            return; // No explicit lifetime, skip to avoid false positives
        }

        // Transient types can safely depend on anything
        if (consumerLifetime == LifetimeRank.Transient)
        {
            return;
        }

        // Find constructors and analyze their parameters
        var constructors = classDeclaration.Members
            .OfType<ConstructorDeclarationSyntax>()
            .Where(c => !c.Modifiers.Any(SyntaxKind.StaticKeyword))
            .ToList();

        // Check primary constructor parameters
        if (classDeclaration.ParameterList != null)
        {
            AnalyzeParameters(
                context,
                classDeclaration.ParameterList.Parameters,
                classSymbol,
                consumerLifetime);
        }

        // Analyze explicit constructor parameters
        foreach (var constructor in constructors)
        {
            AnalyzeParameters(
                context,
                constructor.ParameterList.Parameters,
                classSymbol,
                consumerLifetime);
        }
    }

    private static void AnalyzeParameters(
        SyntaxNodeAnalysisContext context,
        SeparatedSyntaxList<ParameterSyntax> parameters,
        INamedTypeSymbol consumerSymbol,
        LifetimeRank consumerLifetime)
    {
        foreach (var parameter in parameters)
        {
            if (parameter.Type == null)
            {
                continue;
            }

            var typeInfo = context.SemanticModel.GetTypeInfo(parameter.Type);
            var parameterType = typeInfo.Type as INamedTypeSymbol;
            if (parameterType == null)
            {
                continue;
            }

            // Skip factory patterns - these are safe
            if (IsFactoryPattern(parameterType))
            {
                continue;
            }

            // For interfaces, get the concrete type if we can determine it
            var concreteType = GetConcreteTypeForAnalysis(parameterType);
            if (concreteType == null)
            {
                continue; // Can't determine concrete type, skip to avoid false positives
            }

            // Get the EXPLICIT lifetime of the dependency
            var dependencyLifetime = GetExplicitLifetimeFromAttributes(concreteType);
            if (dependencyLifetime == LifetimeRank.Unknown)
            {
                continue; // No explicit lifetime, skip to avoid false positives
            }

            // Check for mismatch: consumer lifetime > dependency lifetime
            if ((int)consumerLifetime <= (int)dependencyLifetime)
            {
                continue; // No mismatch
            }

            // Check if the dependency implements IDisposable or IAsyncDisposable
            var disposableInterface = GetDisposableInterface(concreteType);
            if (disposableInterface == null)
            {
                continue; // Not disposable, let NDLRCOR005 handle generic lifetime mismatch
            }

            var diagnostic = Diagnostic.Create(
                DiagnosticDescriptors.DisposableCaptiveDependency,
                parameter.GetLocation(),
                consumerSymbol.Name,
                GetLifetimeName(consumerLifetime),
                concreteType.Name,
                GetLifetimeName(dependencyLifetime),
                disposableInterface);

            context.ReportDiagnostic(diagnostic);
        }
    }

    /// <summary>
    /// Gets the lifetime ONLY from explicit attributes. Returns Unknown if no explicit attribute.
    /// This is more conservative than LifetimeMismatchAnalyzer which defaults to Singleton.
    /// </summary>
    private static LifetimeRank GetExplicitLifetimeFromAttributes(INamedTypeSymbol typeSymbol)
    {
        foreach (var attribute in typeSymbol.GetAttributes())
        {
            var attributeName = attribute.AttributeClass?.Name;

            if (attributeName is "SingletonAttribute" or "Singleton")
            {
                return LifetimeRank.Singleton;
            }

            if (attributeName is "ScopedAttribute" or "Scoped")
            {
                return LifetimeRank.Scoped;
            }

            if (attributeName is "TransientAttribute" or "Transient")
            {
                return LifetimeRank.Transient;
            }
        }

        return LifetimeRank.Unknown;
    }

    /// <summary>
    /// Check if the type is a factory pattern that safely handles lifetime management.
    /// </summary>
    private static bool IsFactoryPattern(INamedTypeSymbol typeSymbol)
    {
        var name = typeSymbol.Name;
        var fullName = typeSymbol.ToDisplayString();

        // Func<T> - factory delegates
        if (name == "Func" && typeSymbol.IsGenericType)
        {
            return true;
        }

        // Lazy<T> - deferred resolution
        if (name == "Lazy" && typeSymbol.IsGenericType)
        {
            return true;
        }

        // IServiceScopeFactory - scope management
        if (name == "IServiceScopeFactory" || fullName.Contains("IServiceScopeFactory"))
        {
            return true;
        }

        // IServiceProvider - direct resolution
        if (name == "IServiceProvider" || fullName.Contains("IServiceProvider"))
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// Get the concrete type to analyze. For concrete classes, returns the type itself.
    /// For interfaces, returns null (we can't determine the implementation).
    /// </summary>
    private static INamedTypeSymbol? GetConcreteTypeForAnalysis(INamedTypeSymbol typeSymbol)
    {
        // For interfaces, we can't determine the concrete implementation at compile time
        if (typeSymbol.TypeKind == TypeKind.Interface)
        {
            return null;
        }

        // For abstract classes, we can't determine which subclass will be used
        if (typeSymbol.IsAbstract)
        {
            return null;
        }

        return typeSymbol;
    }

    /// <summary>
    /// Check if the type implements IDisposable or IAsyncDisposable.
    /// </summary>
    private static string? GetDisposableInterface(INamedTypeSymbol typeSymbol)
    {
        foreach (var iface in typeSymbol.AllInterfaces)
        {
            var fullName = iface.ToDisplayString();
            if (fullName == "System.IDisposable")
            {
                return "IDisposable";
            }

            if (fullName == "System.IAsyncDisposable")
            {
                return "IAsyncDisposable";
            }
        }

        return null;
    }

    private static string GetLifetimeName(LifetimeRank lifetime) => lifetime switch
    {
        LifetimeRank.Singleton => "Singleton",
        LifetimeRank.Scoped => "Scoped",
        LifetimeRank.Transient => "Transient",
        _ => "Unknown"
    };
}
