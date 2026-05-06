using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace NexusLabs.Needlr.AgentFramework.Analyzers;

/// <summary>
/// Analyzer that hints when an <c>[AgentFunction]</c> <see cref="string"/> parameter is being
/// used to carry JSON (per its name suffix or <c>[Description]</c> text) and could instead be
/// typed as <c>System.Text.Json.JsonElement</c> for direct, typed access.
/// </summary>
/// <remarks>
/// <b>NDLRMAF030</b> (Info): a parameter is named <c>*Json</c>/<c>*_json</c> OR its description
/// mentions <c>"JSON array"</c>/<c>"JSON object"</c>, AND its declared type is <see cref="string"/>.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class AgentFunctionJsonStringParameterAnalyzer : DiagnosticAnalyzer
{
    private const string AgentFunctionAttributeName = "NexusLabs.Needlr.AgentFramework.AgentFunctionAttribute";
    private const string DescriptionAttributeName = "System.ComponentModel.DescriptionAttribute";

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(MafDiagnosticDescriptors.AgentFunctionJsonStringParameter);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSymbolAction(AnalyzeMethod, SymbolKind.Method);
    }

    private static void AnalyzeMethod(SymbolAnalysisContext context)
    {
        var method = (IMethodSymbol)context.Symbol;

        var hasAgentFunction = method.GetAttributes()
            .Any(a => a.AttributeClass?.ToDisplayString() == AgentFunctionAttributeName);

        if (!hasAgentFunction)
            return;

        foreach (var parameter in method.Parameters)
        {
            if (parameter.Type.SpecialType != SpecialType.System_String)
                continue;

            var nameSignal = LooksLikeJsonByName(parameter.Name);
            var descriptionSignal = LooksLikeJsonByDescription(parameter, out var descriptionExcerpt);

            if (!nameSignal && !descriptionSignal)
                continue;

            var reason = nameSignal && descriptionSignal
                ? $"its name '{parameter.Name}' suggests JSON content and its description mentions {descriptionExcerpt}"
                : nameSignal
                    ? $"its name '{parameter.Name}' suggests JSON content"
                    : $"its description mentions {descriptionExcerpt}";

            var location = parameter.Locations.FirstOrDefault() ?? method.Locations[0];

            context.ReportDiagnostic(Diagnostic.Create(
                MafDiagnosticDescriptors.AgentFunctionJsonStringParameter,
                location,
                parameter.Name,
                method.Name,
                reason));
        }
    }

    private static bool LooksLikeJsonByName(string name) =>
        name.EndsWith("Json", StringComparison.Ordinal) ||
        name.EndsWith("_json", StringComparison.Ordinal);

    private static bool LooksLikeJsonByDescription(IParameterSymbol parameter, out string excerpt)
    {
        var descriptionAttr = parameter.GetAttributes()
            .FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == DescriptionAttributeName);

        if (descriptionAttr is null
            || descriptionAttr.ConstructorArguments.Length < 1
            || descriptionAttr.ConstructorArguments[0].Value is not string text)
        {
            excerpt = string.Empty;
            return false;
        }

        if (text.IndexOf("JSON array", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            excerpt = "\"JSON array\"";
            return true;
        }

        if (text.IndexOf("JSON object", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            excerpt = "\"JSON object\"";
            return true;
        }

        excerpt = string.Empty;
        return false;
    }
}
