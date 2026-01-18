using Microsoft.SemanticKernel;

using System.ComponentModel;

/// <summary>
/// This is a common way to create Semantic Kernel plugins so that you can
/// take advantage of dependency injection. Not that this is a non-static
/// class that takes parameters via the constructor.
/// </summary>
internal sealed class CitiesSKFunctionPlugin(
    CitiesProvider _citiesProvider,
    CountriesProvider _countriesProvider)
{
    [KernelFunction("GetCities")]
    [Description("Returns a list of Nick's favorite cities.")]
    public IReadOnlyList<string> GetCities()
    {
        return _citiesProvider.GetCities();
    }

    [KernelFunction("GetCountries")]
    [Description("Returns a list of countries where Nick has lived.")]
    public IReadOnlyList<string> GetCountries()
    {
        return _countriesProvider.GetCountries();
    }
}


