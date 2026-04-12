namespace AvaloniaDemoApp;

/// <summary>
/// A simple service auto-registered by Needlr's source generator.
/// Proves that Needlr correctly discovers and registers application types
/// while excluding Avalonia framework types via ExcludeNamespacePrefixes.
/// </summary>
/// <remarks>
/// This class is in the <c>AvaloniaDemoApp</c> namespace. The source generator
/// scans it because:
/// <list type="bullet">
/// <item><c>NeedlrExcludeNamespacePrefix=Avalonia</c> excludes <c>Avalonia.*</c></item>
/// <item>The default include behavior scans <c>AvaloniaDemoApp.*</c> (the project's RootNamespace)</item>
/// </list>
/// Without the exclusion, Needlr would attempt to register Avalonia's
/// <c>Button</c>, <c>TextBlock</c>, <c>Window</c>, etc. as DI services — which is
/// incorrect and causes build failures.
/// </remarks>
public sealed class GreetingService
{
    public string GetWelcomeMessage()
        => "Needlr source-gen is working with Avalonia + AOT!";

    public string GetGreeting(int clickCount)
        => clickCount switch
        {
            1 => "Hello from Needlr DI!",
            < 5 => $"You've clicked {clickCount} times. Services are flowing!",
            < 10 => $"{clickCount} clicks! Needlr + Avalonia + AOT = cross-platform DI.",
            _ => $"{clickCount} clicks! You're really testing this. It works."
        };
}
