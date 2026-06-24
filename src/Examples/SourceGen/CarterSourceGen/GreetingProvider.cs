namespace CarterSourceGen;

/// <summary>
/// A simple service that the Carter module depends on. It is discovered and registered
/// by Needlr's source generator (no attributes required) and injected into the module's
/// route handler, demonstrating that source-gen DI flows into Carter endpoints.
/// </summary>
internal sealed class GreetingProvider
{
    public string GetGreeting() => "pong";
}
