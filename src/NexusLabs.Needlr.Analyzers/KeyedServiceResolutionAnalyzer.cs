using System.Collections.Concurrent;
using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace NexusLabs.Needlr.Analyzers;

/// <summary>
/// Analyzer that validates [FromKeyedServices] usage against discovered [Keyed] registrations.
/// Reports Info-level diagnostic when a key is not found in statically-discovered registrations.
/// Only active when [assembly: GenerateTypeRegistry] is present.
/// </summary>
/// <remarks>
/// <para>
/// This analyzer collects all [Keyed("key")] attributes from types in the compilation
/// and validates that [FromKeyedServices("key")] parameters reference known keys.
/// </para>
/// <para>
/// Keys registered via plugins at runtime cannot be validated at compile time.
/// Users can suppress this diagnostic for such cases.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class KeyedServiceResolutionAnalyzer : DiagnosticAnalyzer
{
    private const string GenerateTypeRegistryAttributeName = "NexusLabs.Needlr.Generators.GenerateTypeRegistryAttribute";
    private const string FromKeyedServicesAttributeName = "Microsoft.Extensions.DependencyInjection.FromKeyedServicesAttribute";
    private const string KeyedAttributeName = "KeyedAttribute";
    private const string KeyedAttributeFullName = "NexusLabs.Needlr.KeyedAttribute";

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

            // Collect discovered [Keyed] registrations: key -> list of (serviceType, implType)
            var discoveredKeys = new ConcurrentDictionary<string, ConcurrentBag<(string ServiceType, string ImplType)>>();

            // Collect pending usages to validate: (location, serviceType, key)
            var pendingUsages = new ConcurrentBag<(Location Location, string ServiceType, string Key)>();

            // Collect [Keyed] attributes from class declarations
            compilationContext.RegisterSymbolAction(
                ctx => CollectKeyedRegistration(ctx, discoveredKeys),
                SymbolKind.NamedType);

            // Collect [FromKeyedServices] parameters
            compilationContext.RegisterSyntaxNodeAction(
                ctx => CollectKeyedServiceParameter(ctx, fromKeyedServicesType, pendingUsages),
                SyntaxKind.Parameter);

            // Validate at compilation end
            compilationContext.RegisterCompilationEndAction(endContext =>
            {
                foreach (var (location, serviceType, key) in pendingUsages)
                {
                    // Check if key is found in discovered registrations
                    if (discoveredKeys.TryGetValue(key, out var registrations))
                    {
                        // Key exists - check if any registration matches the service type
                        // For now, just having the key is enough (interface matching is complex)
                        continue;
                    }

                    // Key not found in statically-discovered registrations
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

    private static void CollectKeyedRegistration(
        SymbolAnalysisContext context,
        ConcurrentDictionary<string, ConcurrentBag<(string, string)>> discoveredKeys)
    {
        var typeSymbol = (INamedTypeSymbol)context.Symbol;

        // Skip non-classes
        if (typeSymbol.TypeKind != TypeKind.Class)
            return;

        // Look for [Keyed] attributes
        foreach (var attr in typeSymbol.GetAttributes())
        {
            var attrClass = attr.AttributeClass;
            if (attrClass == null)
                continue;

            var name = attrClass.Name;
            var fullName = attrClass.ToDisplayString();

            if (name != KeyedAttributeName && fullName != KeyedAttributeFullName)
                continue;

            // Extract the key from the constructor argument
            if (attr.ConstructorArguments.Length == 0)
                continue;

            var keyArg = attr.ConstructorArguments[0];
            if (keyArg.Value is not string key)
                continue;

            // Get the interfaces this type implements
            var implTypeName = typeSymbol.ToDisplayString();
            foreach (var iface in typeSymbol.AllInterfaces)
            {
                // Skip framework interfaces
                var ns = iface.ContainingNamespace?.ToDisplayString() ?? "";
                if (ns.StartsWith("System") || ns.StartsWith("Microsoft"))
                    continue;

                var bag = discoveredKeys.GetOrAdd(key, _ => new ConcurrentBag<(string, string)>());
                bag.Add((iface.ToDisplayString(), implTypeName));
            }

            // Also add self-registration
            var selfBag = discoveredKeys.GetOrAdd(key, _ => new ConcurrentBag<(string, string)>());
            selfBag.Add((implTypeName, implTypeName));
        }
    }

    private static void CollectKeyedServiceParameter(
        SyntaxNodeAnalysisContext context,
        INamedTypeSymbol fromKeyedServicesType,
        ConcurrentBag<(Location, string, string)> pendingUsages)
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

            pendingUsages.Add((parameter.GetLocation(), typeName, key));
            break;
        }
    }
}
