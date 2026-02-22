namespace SimpleAgentFrameworkApp;

/// <summary>
/// Provides personal data injected into function classes via Needlr's DI wiring.
/// Registered as a singleton in DI.
/// </summary>
internal sealed class PersonalDataProvider
{
    public IReadOnlyList<string> GetCities() =>
        ["London", "Tokyo", "Barcelona", "Vancouver", "Amsterdam"];

    public IReadOnlyList<string> GetCountries() =>
        ["Canada", "United Kingdom", "Japan"];
}
