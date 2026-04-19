using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace NexusLabs.Needlr.AgentFramework.Analyzers;

/// <summary>
/// Detects <c>.ToString()</c> invocations on <c>ToolCallResult.Result</c> and
/// <c>FunctionResultContent.Result</c> properties, which are <c>object?</c>
/// and may contain a <c>JsonElement</c> at runtime.
/// </summary>
/// <remarks>
/// <b>NDLRMAF015</b> (Warning): Calling <c>ToString()</c> on these properties
/// produces a C# type name for complex objects instead of JSON. Developers
/// should use <c>ToolResultSerializer.Serialize()</c> instead.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ToolResultToStringAnalyzer : DiagnosticAnalyzer
{
    private static readonly ImmutableHashSet<string> TargetTypeNames = ImmutableHashSet.Create(
        "NexusLabs.Needlr.AgentFramework.Iterative.ToolCallResult",
        "Microsoft.Extensions.AI.FunctionResultContent");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(MafDiagnosticDescriptors.ToolResultToStringCall);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterOperationAction(AnalyzeInvocation, OperationKind.Invocation);
    }

    private static void AnalyzeInvocation(OperationAnalysisContext context)
    {
        var invocation = (IInvocationOperation)context.Operation;

        // We're looking for .ToString() calls
        if (invocation.TargetMethod.Name != "ToString" ||
            invocation.TargetMethod.Parameters.Length != 0)
        {
            return;
        }

        // The receiver must be a property access to .Result
        // This handles both direct access (result.Result.ToString())
        // and null-conditional access (result.Result?.ToString())
        IPropertyReferenceOperation? propertyRef = null;

        if (invocation.Instance is IPropertyReferenceOperation directProp)
        {
            propertyRef = directProp;
        }
        else if (invocation.Instance is IConditionalAccessInstanceOperation)
        {
            // For result.Result?.ToString(), walk up to the ConditionalAccessOperation
            // and check its operand
            var parent = invocation.Parent;
            while (parent is not null and not IConditionalAccessOperation)
            {
                parent = parent.Parent;
            }

            if (parent is IConditionalAccessOperation conditional &&
                conditional.Operation is IPropertyReferenceOperation condProp)
            {
                propertyRef = condProp;
            }
        }

        if (propertyRef is null || propertyRef.Property.Name != "Result")
        {
            return;
        }

        // Check if the containing type is one of our targets
        var containingType = propertyRef.Property.ContainingType?.ToDisplayString();
        if (containingType is null || !TargetTypeNames.Contains(containingType))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(
            MafDiagnosticDescriptors.ToolResultToStringCall,
            invocation.Syntax.GetLocation(),
            containingType));
    }
}
