using System.ComponentModel;

using NexusLabs.Needlr.AgentFramework;

namespace DevUIApp;

/// <summary>
/// Simple data tools for the DataAssistant agent.
/// </summary>
[AgentFunctionGroup("data-tools")]
public static class DataFunctions
{
    [AgentFunction]
    [Description("Returns the current server time in UTC.")]
    public static string GetCurrentTime() => DateTimeOffset.UtcNow.ToString("O");

    [AgentFunction]
    [Description("Looks up a capital city for a given country name.")]
    public static string LookupCapital(string country)
    {
        var capitals = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["canada"] = "Ottawa",
            ["france"] = "Paris",
            ["japan"] = "Tokyo",
            ["brazil"] = "Brasília",
            ["australia"] = "Canberra",
        };

        return capitals.TryGetValue(country, out var capital)
            ? $"The capital of {country} is {capital}."
            : $"Unknown country: '{country}'.";
    }
}
