using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace NexusLabs.Needlr.SignalR.Analyzers;

/// <summary>
/// Analyzer that validates HubPathAttribute usage for AOT compatibility.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class HubPathAttributeAnalyzer : DiagnosticAnalyzer
{
    private const string HubPathAttributeName = "HubPathAttribute";
    private const string HubPathAttributeShortName = "HubPath";

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(
            DiagnosticDescriptors.HubPathMustBeConstant,
            DiagnosticDescriptors.HubTypeMustBeTypeOf);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterSyntaxNodeAction(AnalyzeAttribute, SyntaxKind.Attribute);
    }

    private static void AnalyzeAttribute(SyntaxNodeAnalysisContext context)
    {
        var attributeSyntax = (AttributeSyntax)context.Node;

        // Check if this is a HubPathAttribute
        var attributeName = GetAttributeName(attributeSyntax);
        if (attributeName != HubPathAttributeName && attributeName != HubPathAttributeShortName)
        {
            return;
        }

        // Verify it's actually the Needlr HubPathAttribute via semantic model
        var symbolInfo = context.SemanticModel.GetSymbolInfo(attributeSyntax, context.CancellationToken);
        if (symbolInfo.Symbol is not IMethodSymbol attributeConstructor)
        {
            return;
        }

        var attributeType = attributeConstructor.ContainingType;
        if (attributeType?.ContainingNamespace?.ToDisplayString() != "NexusLabs.Needlr.SignalR")
        {
            return;
        }

        // Analyze the attribute arguments
        var argumentList = attributeSyntax.ArgumentList;
        if (argumentList == null)
        {
            return;
        }

        foreach (var argument in argumentList.Arguments)
        {
            var parameterName = GetParameterName(argument, context.SemanticModel);

            if (parameterName == "hubPath" || (parameterName == null && IsFirstPositionalArgument(argument, argumentList)))
            {
                AnalyzeHubPathArgument(context, argument);
            }
            else if (parameterName == "hubType" || (parameterName == null && IsSecondPositionalArgument(argument, argumentList)))
            {
                AnalyzeHubTypeArgument(context, argument);
            }
        }
    }

    private static string? GetAttributeName(AttributeSyntax attribute)
    {
        return attribute.Name switch
        {
            IdentifierNameSyntax identifier => identifier.Identifier.Text,
            QualifiedNameSyntax qualified => qualified.Right.Identifier.Text,
            _ => null
        };
    }

    private static string? GetParameterName(AttributeArgumentSyntax argument, SemanticModel semanticModel)
    {
        return argument.NameEquals?.Name.Identifier.Text ?? argument.NameColon?.Name.Identifier.Text;
    }

    private static bool IsFirstPositionalArgument(AttributeArgumentSyntax argument, AttributeArgumentListSyntax argumentList)
    {
        if (argument.NameEquals != null || argument.NameColon != null)
        {
            return false;
        }

        var positionalArgs = argumentList.Arguments.Where(a => a.NameEquals == null && a.NameColon == null).ToList();
        return positionalArgs.Count > 0 && positionalArgs[0] == argument;
    }

    private static bool IsSecondPositionalArgument(AttributeArgumentSyntax argument, AttributeArgumentListSyntax argumentList)
    {
        if (argument.NameEquals != null || argument.NameColon != null)
        {
            return false;
        }

        var positionalArgs = argumentList.Arguments.Where(a => a.NameEquals == null && a.NameColon == null).ToList();
        return positionalArgs.Count > 1 && positionalArgs[1] == argument;
    }

    private static void AnalyzeHubPathArgument(SyntaxNodeAnalysisContext context, AttributeArgumentSyntax argument)
    {
        var expression = argument.Expression;

        // Check if it's a constant expression
        var constantValue = context.SemanticModel.GetConstantValue(expression, context.CancellationToken);

        if (!constantValue.HasValue)
        {
            // Not a constant - report diagnostic
            var expressionText = expression.ToString();
            var diagnostic = Diagnostic.Create(
                DiagnosticDescriptors.HubPathMustBeConstant,
                expression.GetLocation(),
                expressionText);

            context.ReportDiagnostic(diagnostic);
        }
    }

    private static void AnalyzeHubTypeArgument(SyntaxNodeAnalysisContext context, AttributeArgumentSyntax argument)
    {
        var expression = argument.Expression;

        // HubType must be a typeof expression
        if (expression is not TypeOfExpressionSyntax)
        {
            var expressionText = expression.ToString();
            var diagnostic = Diagnostic.Create(
                DiagnosticDescriptors.HubTypeMustBeTypeOf,
                expression.GetLocation(),
                expressionText);

            context.ReportDiagnostic(diagnostic);
        }
    }
}
