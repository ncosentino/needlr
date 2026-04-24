using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Linq;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace NexusLabs.Needlr.AgentFramework.Analyzers;

/// <summary>
/// Detects calls to <c>CreateGraphWorkflow</c> (which returns a MAF <c>Workflow</c>
/// using BSP execution) when the compilation also declares
/// <c>[AgentGraphNode(JoinMode = GraphJoinMode.WaitAny)]</c> for any graph.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class WaitAnyCreateGraphAnalyzer : DiagnosticAnalyzer
{
    private const string AgentGraphNodeAttributeName = "NexusLabs.Needlr.AgentFramework.AgentGraphNodeAttribute";
    private const string CreateGraphWorkflowMethodName = "CreateGraphWorkflow";

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(MafDiagnosticDescriptors.WaitAnyIncompatibleWithCreateGraphWorkflow);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(compilationContext =>
        {
            var waitAnyGraphNames = new ConcurrentBag<string>();
            var createGraphCalls = new ConcurrentBag<(Location Location, string GraphName)>();

            compilationContext.RegisterSymbolAction(symbolContext =>
            {
                var namedType = (INamedTypeSymbol)symbolContext.Symbol;
                foreach (var attr in namedType.GetAttributes())
                {
                    if (attr.AttributeClass?.ToDisplayString() != AgentGraphNodeAttributeName)
                        continue;

                    string? graphName = null;
                    if (attr.ConstructorArguments.Length >= 1 &&
                        attr.ConstructorArguments[0].Value is string gn)
                    {
                        graphName = gn;
                    }

                    foreach (var named in attr.NamedArguments)
                    {
                        if (named.Key == "JoinMode" &&
                            named.Value.Value is int joinModeValue &&
                            joinModeValue == 1)
                        {
                            if (graphName is not null)
                            {
                                waitAnyGraphNames.Add(graphName);
                            }
                        }
                    }
                }
            }, SymbolKind.NamedType);

            compilationContext.RegisterSyntaxNodeAction(syntaxContext =>
            {
                var invocation = (InvocationExpressionSyntax)syntaxContext.Node;

                string? methodName = null;
                if (invocation.Expression is MemberAccessExpressionSyntax memberAccess)
                {
                    methodName = memberAccess.Name.Identifier.Text;
                }
                else if (invocation.Expression is IdentifierNameSyntax identifier)
                {
                    methodName = identifier.Identifier.Text;
                }

                if (methodName != CreateGraphWorkflowMethodName)
                    return;

                var symbolInfo = syntaxContext.SemanticModel.GetSymbolInfo(invocation, syntaxContext.CancellationToken);
                if (symbolInfo.Symbol is not IMethodSymbol methodSymbol)
                    return;

                var containingType = methodSymbol.ContainingType;
                if (containingType?.Name != "IWorkflowFactory" &&
                    containingType?.ToDisplayString() != "NexusLabs.Needlr.AgentFramework.IWorkflowFactory")
                    return;

                if (invocation.ArgumentList.Arguments.Count < 1)
                    return;

                var firstArg = invocation.ArgumentList.Arguments[0].Expression;
                var constantValue = syntaxContext.SemanticModel.GetConstantValue(firstArg);
                if (constantValue.HasValue && constantValue.Value is string graphName)
                {
                    createGraphCalls.Add((invocation.GetLocation(), graphName));
                }
            }, SyntaxKind.InvocationExpression);

            compilationContext.RegisterCompilationEndAction(endContext =>
            {
                var waitAnyNames = new System.Collections.Generic.HashSet<string>(waitAnyGraphNames);
                foreach (var (location, graphName) in createGraphCalls)
                {
                    if (waitAnyNames.Contains(graphName))
                    {
                        endContext.ReportDiagnostic(Diagnostic.Create(
                            MafDiagnosticDescriptors.WaitAnyIncompatibleWithCreateGraphWorkflow,
                            location,
                            graphName));
                    }
                }
            });
        });
    }
}
