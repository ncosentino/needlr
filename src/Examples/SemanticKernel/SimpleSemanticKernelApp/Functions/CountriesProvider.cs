using NexusLabs.Needlr;
/// <summary>
/// An example of a type that we can manually register with a plugin.
/// </summary>
[DoNotAutoRegister]
internal sealed class CountriesProvider
{
    public IReadOnlyList<string> GetCountries()
    {
        return new List<string>
        {
            "France",
            "United Kingdom",
            "United States",
            "Japan"
        };
    }
}


