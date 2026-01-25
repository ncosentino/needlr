using System.Collections.Concurrent;
using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace NexusLabs.Needlr.Analyzers;

/// <summary>
/// Analyzer that detects [FromKeyedServices] usage and reports informational diagnostics
/// about keyed service dependencies. Only active when [assembly: GenerateTypeRegistry] is present.
/// </summary>
/// <remarks>
/// Keyed services are typically registered via IServiceCollectionPlugin at runtime,
/// so this analyzer cannot validate that keys are actually registered. It provides
/// awareness of keyed service usage patterns.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class KeyedServiceResolutionAnalyzer : DiagnosticAnalyzer
{
    private const string GenerateTypeRegistryAttributeName = "NexusLabs.Needlr.Generators.GenerateTypeRegistryAttribute";
    private const string FromKeyedServicesAttributeName = "Microsoft.Extensions.DependencyInjection.FromKeyedServicesAttribute";

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(DiagnosticDescriptors.KeyedServiceUnknownKey);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(compilationContext =>
        {
            if (!HasGenerateTypeRegistryAttribute(compilationContext.Compilation))
                return;

            var fromKeyedServicesType = compilationContext.Compilation.GetTypeByMetadataName(FromKeyedServicesAttributeName);
            if (fromKeyedServicesType == null)
                return;

            // Collect pending diagnostics to report at compilation end
            var pendingDiagnostics = new ConcurrentBag<(Location Location, string ServiceType, string Key)>();

            // Collect [FromKeyedServices] parameters
            compilationContext.RegisterSyntaxNodeAction(
                ctx => CollectKeyedServiceParameter(ctx, fromKeyedServicesType, pendingDiagnostics),
                SyntaxKind.Parameter);

            // Report diagnostics at compilation end
            compilationContext.RegisterCompilationEndAction(endContext =>
            {
                foreach (var (location, serviceType, key) in pendingDiagnostics)
                {
                    var diagnostic = Diagnostic.Create(
                        DiagnosticDescriptors.KeyedServiceUnknownKey,
                        location,
                        serviceType,
                        key);

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

    private static void CollectKeyedServiceParameter(
        SyntaxNodeAnalysisContext context,
        INamedTypeSymbol fromKeyedServicesType,
        ConcurrentBag<(Location, string, string)> pendingDiagnostics)
    {
        var parameter = (ParameterSyntax)context.Node;

        // Get the parameter symbol to access attributes
        var parameterSymbol = context.SemanticModel.GetDeclaredSymbol(parameter);
        if (parameterSymbol == null)
            return;

        // Look for [FromKeyedServices] attribute
        foreach (var attr in parameterSymbol.GetAttributes())
        {
            if (!SymbolEqualityComparer.Default.Equals(attr.AttributeClass, fromKeyedServicesType))
                continue;

            // Extract the key from the constructor argument
            if (attr.ConstructorArguments.Length == 0)
                continue;

            var keyArg = attr.ConstructorArguments[0];
            if (keyArg.Value is not string key)
                continue;

            // Get the parameter type
            var parameterType = parameterSymbol.Type;
            var typeName = parameterType.Name;

            // Skip framework types
            var ns = parameterType.ContainingNamespace?.ToDisplayString() ?? "";
            if (ns.StartsWith("Microsoft.Extensions.") ||
                ns.StartsWith("Microsoft.AspNetCore.") ||
                ns == "System" ||
                ns.StartsWith("System."))
            {
                continue;
            }

            pendingDiagnostics.Add((parameter.GetLocation(), typeName, key));
            break;
        }
    }
}
