using System.ComponentModel;

using NexusLabs.Needlr.AgentFramework;
using NexusLabs.Needlr.AgentFramework.Tools;

namespace SimpleAgentFrameworkApp.Agents;

/// <summary>
/// Instance-based function class — non-static so that <see cref="PersonalDataProvider"/>
/// can be injected by Needlr's DI wiring.
/// </summary>
/// <remarks>
/// <see cref="GetCountriesLived"/> demonstrates <see cref="ToolResult{TValue, TError}"/>:
/// a structured return type that separates the LLM-facing value from C#-facing exception
/// context. The <c>ToolResultFunctionMiddleware</c> handles serialisation automatically.
/// </remarks>
[AgentFunctionGroup("geography")]
internal sealed class GeographyFunctions(PersonalDataProvider dataProvider)
{
    [AgentFunction]
    [Description("Returns a list of Nick's favorite cities.")]
    public IReadOnlyList<string> GetFavoriteCities() => dataProvider.GetCities();

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
