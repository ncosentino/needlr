using Microsoft.SemanticKernel;

using System.ComponentModel;

/// <summary>
/// This is a simple way to create Semantic Kernel plugins when you
/// do not need dependency injection. Note that this is a static
/// class with static methods.
/// </summary>
internal static class StaticSkFunctionsPlugin
{
    [KernelFunction("GetIcecream")]
    [Description("Returns a list of Nick's favorite icecream.")]
    public static IReadOnlyList<string> GetIcecream()
    {
        return new List<string>
        {
            "Chocolate",
            "Vanilla",
            "Double Chocolate",
            "Cookie Dough"
        };
    }
}


