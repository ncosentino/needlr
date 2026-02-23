namespace SimpleAgentFrameworkApp.Agents;

/// <summary>
/// Provides personal data injected into function classes via Needlr's DI wiring.
/// </summary>
internal sealed class PersonalDataProvider
{
    public IReadOnlyList<string> GetCities() =>
        ["London", "Tokyo", "Barcelona", "Vancouver", "Amsterdam"];

    public IReadOnlyList<string> GetCountries() =>
        ["Canada", "United Kingdom", "Japan"];
}
