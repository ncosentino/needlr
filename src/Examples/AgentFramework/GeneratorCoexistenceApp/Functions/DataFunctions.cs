using System.ComponentModel;

using NexusLabs.Needlr.AgentFramework;

namespace GeneratorCoexistenceApp;

/// <summary>
/// Needlr-side: <see cref="AgentFunctionGroupAttribute"/> declares this class
/// as a tool group. Needlr's source generator emits <c>AIFunction</c> registrations
/// and a <c>GeneratedAIFunctionProvider</c> for DI resolution.
/// </summary>
[AgentFunctionGroup("data-tools")]
public static class DataFunctions
{
    [AgentFunction]
    [Description("Looks up a value from the in-memory data store by key.")]
    public static string LookupValue(string key)
    {
        var store = new Dictionary<string, string>
        {
            ["capital-canada"] = "Ottawa",
            ["capital-france"] = "Paris",
            ["capital-japan"] = "Tokyo",
        };

        return store.TryGetValue(key, out var value)
            ? value
            : $"No value found for key '{key}'.";
    }

    [AgentFunction]
    [Description("Returns the current UTC date and time.")]
    public static string GetCurrentTime() => DateTimeOffset.UtcNow.ToString("O");
}
