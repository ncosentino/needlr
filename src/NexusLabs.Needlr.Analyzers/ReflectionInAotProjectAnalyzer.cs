using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace NexusLabs.Needlr.Analyzers;

/// <summary>
/// Analyzer that detects reflection-based Needlr API usage in AOT-enabled projects.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ReflectionInAotProjectAnalyzer : DiagnosticAnalyzer
{
    // Reflection-based method names that should trigger the diagnostic
    private static readonly ImmutableHashSet<string> ReflectionMethodNames = ImmutableHashSet.Create(
        "UsingReflectionTypeRegistrar",
        "UsingReflectionTypeFilterer",
        "UsingReflectionPluginFactory",
        "UsingReflectionAssemblyLoader",
        "UsingReflectionAssemblyProvider");

    // Reflection-based type names that should trigger the diagnostic
    private static readonly ImmutableHashSet<string> ReflectionTypeNames = ImmutableHashSet.Create(
        "ReflectionPluginFactory",
        "ReflectionTypeRegistrar",
        "ReflectionTypeFilterer",
        "ReflectionAssemblyLoader",
        "ReflectionAssemblyProvider",
        "ReflectionServiceProviderBuilder");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(DiagnosticDescriptors.ReflectionInAotProject);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(compilationContext =>
        {
            // Check if project has AOT/trimming enabled
            if (!IsAotEnabledProject(compilationContext.Options))
            {
                return;
            }

            // Register syntax node analysis for method invocations
            compilationContext.RegisterSyntaxNodeAction(
                AnalyzeInvocation,
                SyntaxKind.InvocationExpression);

            // Register syntax node analysis for object creation
            compilationContext.RegisterSyntaxNodeAction(
                AnalyzeObjectCreation,
                SyntaxKind.ObjectCreationExpression);
        });
    }

    private static bool IsAotEnabledProject(AnalyzerOptions options)
    {
        // Check for PublishAot or IsAotCompatible in analyzer config
        var globalOptions = options.AnalyzerConfigOptionsProvider.GlobalOptions;

        if (globalOptions.TryGetValue("build_property.PublishAot", out var publishAot) &&
            string.Equals(publishAot, "true", System.StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (globalOptions.TryGetValue("build_property.IsAotCompatible", out var isAotCompatible) &&
            string.Equals(isAotCompatible, "true", System.StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }

    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;

        // Get the method name being invoked
        string? methodName = null;
        Location? location = null;

        if (invocation.Expression is MemberAccessExpressionSyntax memberAccess)
        {
            methodName = memberAccess.Name.Identifier.Text;
            location = memberAccess.Name.GetLocation();
        }
        else if (invocation.Expression is IdentifierNameSyntax identifier)
        {
            methodName = identifier.Identifier.Text;
            location = identifier.GetLocation();
        }

        if (methodName != null && location != null && ReflectionMethodNames.Contains(methodName))
        {
            var diagnostic = Diagnostic.Create(
                DiagnosticDescriptors.ReflectionInAotProject,
                location,
                methodName);

            context.ReportDiagnostic(diagnostic);
        }
    }

    private static void AnalyzeObjectCreation(SyntaxNodeAnalysisContext context)
    {
        var objectCreation = (ObjectCreationExpressionSyntax)context.Node;

        // Get the type name being instantiated
        string? typeName = null;
        Location? location = null;

        if (objectCreation.Type is IdentifierNameSyntax identifier)
        {
            typeName = identifier.Identifier.Text;
            location = identifier.GetLocation();
        }
        else if (objectCreation.Type is QualifiedNameSyntax qualifiedName)
        {
            typeName = qualifiedName.Right.Identifier.Text;
            location = qualifiedName.Right.GetLocation();
        }

        if (typeName != null && location != null && ReflectionTypeNames.Contains(typeName))
        {
            var diagnostic = Diagnostic.Create(
                DiagnosticDescriptors.ReflectionInAotProject,
                location,
                typeName);

            context.ReportDiagnostic(diagnostic);
        }
    }
}
