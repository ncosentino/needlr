using System.ComponentModel;

using NexusLabs.Needlr.AgentFramework;

namespace SimpleAgentFrameworkApp.Agents;

/// <summary>
/// Instance-based function class — non-static so that <see cref="PersonalDataProvider"/>
/// can be injected by Needlr's DI wiring.
/// </summary>
[AgentFunctionGroup("geography")]
internal sealed class GeographyFunctions(PersonalDataProvider dataProvider)
{
    [AgentFunction]
    [Description("Returns a list of Nick's favorite cities.")]
    public IReadOnlyList<string> GetFavoriteCities() => dataProvider.GetCities();

    [AgentFunction]
    [Description("Returns a list of countries where Nick has lived.")]
    public IReadOnlyList<string> GetCountriesLived() => dataProvider.GetCountries();
}
