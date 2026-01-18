
/// <summary>
/// An example of a type that will get automatically picked up by Needlr
/// when scanning for types across assemblies.
/// </summary>
internal sealed class CitiesProvider
{
    public IReadOnlyList<string> GetCities()
    {
        return new List<string>
        {
            "Paris",
            "London",
            "New York",
            "Tokyo"
        };
    }
}


