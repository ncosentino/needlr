using System.ComponentModel;

using NexusLabs.Needlr.AgentFramework;
using NexusLabs.Needlr.AgentFramework.Context;
using NexusLabs.Needlr.AgentFramework.Diagnostics;
using NexusLabs.Needlr.AgentFramework.Tools;

namespace SimpleAgentFrameworkApp.Agents;

/// <summary>
/// Instance-based function class — non-static so that <see cref="PersonalDataProvider"/>
/// and <see cref="IAgentExecutionContextAccessor"/> can be injected by Needlr's DI wiring.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="GetCountriesLived"/> demonstrates <see cref="ToolResult{TValue, TError}"/>:
/// a structured return type that separates the LLM-facing value from C#-facing exception
/// context. The <c>ToolResultFunctionMiddleware</c> handles serialisation automatically.
/// </para>
/// <para>
/// <see cref="GetFavoriteCities"/> demonstrates reading from <see cref="IAgentExecutionContextAccessor"/>
/// and attaching custom metrics via <see cref="IToolMetricsAccessor.AttachMetric"/>:
/// the trusted caller (Program.cs) sets the user identity via <c>BeginScope()</c>, and the
/// tool reads it via <c>GetRequired()</c> — never from LLM-provided parameters.
/// </para>
/// </remarks>
[AgentFunctionGroup("geography")]
internal sealed class GeographyFunctions(
    PersonalDataProvider dataProvider,
    IAgentExecutionContextAccessor contextAccessor,
    IToolMetricsAccessor toolMetrics)
{
    [AgentFunction]
    [Description("Returns a list of Nick's favorite cities and identifies who is asking.")]
    public string GetFavoriteCities()
    {
        var ctx = contextAccessor.GetRequired();
        var cities = dataProvider.GetCities();

        // Attach custom metrics — visible in diagnostics ToolCallDiagnostics.CustomMetrics
        toolMetrics.AttachMetric("source", "in-memory");
        toolMetrics.AttachMetric("city_count", cities.Count);

        return $"[Requested by {ctx.UserId} (orch: {ctx.OrchestrationId})]: {string.Join(", ", cities)}";
    }

    [AgentFunction]
    [Description("Returns a list of countries where Nick has lived.")]
    public ToolResult<IReadOnlyList<string>, ToolError> GetCountriesLived()
    {
        try
        {
            return ToolResult.Ok<IReadOnlyList<string>>(dataProvider.GetCountries());
        }
        catch (Exception ex)
        {
            return ToolResult.Fail<IReadOnlyList<string>>(
                "Unable to retrieve country data.",
                ex: ex,
                isTransient: true,
                suggestion: "Try again shortly.");
        }
    }
}
