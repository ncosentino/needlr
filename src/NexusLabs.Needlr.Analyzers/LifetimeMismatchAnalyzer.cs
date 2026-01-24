using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace NexusLabs.Needlr.Analyzers;

/// <summary>
/// Analyzer that detects lifetime mismatches in service registrations.
/// A lifetime mismatch occurs when a longer-lived service depends on a shorter-lived service.
/// </summary>
/// <remarks>
/// Examples of mismatches:
/// - Singleton depends on Scoped → captive dependency
/// - Singleton depends on Transient → captive dependency
/// - Scoped depends on Transient → captive dependency
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class LifetimeMismatchAnalyzer : DiagnosticAnalyzer
{
    // Lifetime ranking: higher number = longer lifetime
    private enum LifetimeRank
    {
        Unknown = -1,
        Transient = 0,
        Scoped = 1,
        Singleton = 2
    }

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(DiagnosticDescriptors.LifetimeMismatch);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterSyntaxNodeAction(AnalyzeClassDeclaration, SyntaxKind.ClassDeclaration);
    }

    private static void AnalyzeClassDeclaration(SyntaxNodeAnalysisContext context)
    {
        var classDeclaration = (ClassDeclarationSyntax)context.Node;

        // Skip abstract classes
        if (classDeclaration.Modifiers.Any(SyntaxKind.AbstractKeyword))
        {
            return;
        }

        var classSymbol = context.SemanticModel.GetDeclaredSymbol(classDeclaration);
        if (classSymbol == null)
        {
            return;
        }

        // Get the lifetime of this class from attributes
        var consumerLifetime = GetLifetimeFromAttributes(classSymbol);
        if (consumerLifetime == LifetimeRank.Unknown)
        {
            return; // Not a registered service or unknown lifetime
        }

        // Find constructors and analyze their parameters
        var constructors = classDeclaration.Members
            .OfType<ConstructorDeclarationSyntax>()
            .Where(c => !c.Modifiers.Any(SyntaxKind.StaticKeyword))
            .ToList();

        // If no explicit constructors, check primary constructor parameters
        if (classDeclaration.ParameterList != null)
        {
            AnalyzeParameters(
                context,
                classDeclaration.ParameterList.Parameters,
                classSymbol,
                consumerLifetime,
                classDeclaration.Identifier.GetLocation());
        }

        // Analyze explicit constructor parameters
        foreach (var constructor in constructors)
        {
            AnalyzeParameters(
                context,
                constructor.ParameterList.Parameters,
                classSymbol,
                consumerLifetime,
                constructor.Identifier.GetLocation());
        }
    }

    private static void AnalyzeParameters(
        SyntaxNodeAnalysisContext context,
        SeparatedSyntaxList<ParameterSyntax> parameters,
        INamedTypeSymbol consumerSymbol,
        LifetimeRank consumerLifetime,
        Location reportLocation)
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

            // Get the lifetime of the dependency type
            var dependencyLifetime = GetLifetimeFromServiceType(parameterType);
            if (dependencyLifetime == LifetimeRank.Unknown)
            {
                continue; // Unknown lifetime, skip
            }

            // Check for mismatch: consumer lifetime > dependency lifetime
            if ((int)consumerLifetime > (int)dependencyLifetime)
            {
                var diagnostic = Diagnostic.Create(
                    DiagnosticDescriptors.LifetimeMismatch,
                    parameter.GetLocation(),
                    consumerSymbol.Name,
                    GetLifetimeName(consumerLifetime),
                    parameterType.Name,
                    GetLifetimeName(dependencyLifetime));

                context.ReportDiagnostic(diagnostic);
            }
        }
    }

    private static LifetimeRank GetLifetimeFromAttributes(INamedTypeSymbol typeSymbol)
    {
        foreach (var attribute in typeSymbol.GetAttributes())
        {
            var attributeName = attribute.AttributeClass?.Name;

            // Check for RegisterAsAttribute with lifetime parameter
            if (attributeName is "RegisterAsAttribute" or "RegisterAs")
            {
                // First constructor argument is typically the lifetime
                if (attribute.ConstructorArguments.Length > 0)
                {
                    var arg = attribute.ConstructorArguments[0];
                    if (arg.Type?.Name == "ServiceLifetime" && arg.Value is int lifetimeValue)
                    {
                        return lifetimeValue switch
                        {
                            0 => LifetimeRank.Singleton,
                            1 => LifetimeRank.Scoped,
                            2 => LifetimeRank.Transient,
                            _ => LifetimeRank.Unknown
                        };
                    }
                }

                // Check named arguments
                foreach (var namedArg in attribute.NamedArguments)
                {
                    if (namedArg.Key == "Lifetime" && namedArg.Value.Value is int namedLifetimeValue)
                    {
                        return namedLifetimeValue switch
                        {
                            0 => LifetimeRank.Singleton,
                            1 => LifetimeRank.Scoped,
                            2 => LifetimeRank.Transient,
                            _ => LifetimeRank.Unknown
                        };
                    }
                }

                // Default to Transient if no lifetime specified
                return LifetimeRank.Transient;
            }

            // Check for specific lifetime attributes
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

            // Check for AutoRegister with lifetime
            if (attributeName is "AutoRegisterAttribute" or "AutoRegister")
            {
                foreach (var namedArg in attribute.NamedArguments)
                {
                    if (namedArg.Key == "Lifetime" && namedArg.Value.Value is int autoRegLifetimeValue)
                    {
                        return autoRegLifetimeValue switch
                        {
                            0 => LifetimeRank.Singleton,
                            1 => LifetimeRank.Scoped,
                            2 => LifetimeRank.Transient,
                            _ => LifetimeRank.Unknown
                        };
                    }
                }
            }
        }

        return LifetimeRank.Unknown;
    }

    private static LifetimeRank GetLifetimeFromServiceType(INamedTypeSymbol serviceType)
    {
        // For interfaces/abstract types, we need to find implementations
        // This simplified approach only checks direct type attributes
        // Full implementation would use compilation-level analysis

        // First, check if the type itself has lifetime attributes (concrete type)
        var directLifetime = GetLifetimeFromAttributes(serviceType);
        if (directLifetime != LifetimeRank.Unknown)
        {
            return directLifetime;
        }

        // For interfaces, we cannot reliably determine lifetime at syntax level
        // The runtime verification will catch these cases
        return LifetimeRank.Unknown;
    }

    private static string GetLifetimeName(LifetimeRank lifetime) => lifetime switch
    {
        LifetimeRank.Singleton => "Singleton",
        LifetimeRank.Scoped => "Scoped",
        LifetimeRank.Transient => "Transient",
        _ => "Unknown"
    };
}
