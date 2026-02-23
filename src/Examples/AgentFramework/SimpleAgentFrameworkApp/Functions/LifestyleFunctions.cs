using System.ComponentModel;

using NexusLabs.Needlr.AgentFramework;

namespace SimpleAgentFrameworkApp;

/// <summary>
/// Static function class — no DI dependencies needed, all data is hard-coded.
/// </summary>
[AgentFunctionGroup("research")]
internal static class LifestyleFunctions
{
    [AgentFunction]
    [Description("Returns a list of Nick's hobbies.")]
    public static IReadOnlyList<string> GetHobbies() =>
        ["Coding", "Hiking", "Piano", "Cooking", "Photography"];

    [AgentFunction]
    [Description("Returns Nick's favorite ice cream flavors.")]
    public static IReadOnlyList<string> GetFavoriteIceCream() =>
        ["Chocolate", "Cookie Dough", "Mint Chip"];
}
