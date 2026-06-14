namespace NexusLabs.Needlr.Maui.Tests;

/// <summary>
/// A test service implementation that Needlr discovers and registers as <see cref="ITestGreeter"/>.
/// </summary>
public sealed class TestGreeter : ITestGreeter
{
    /// <inheritdoc />
    public string Greet() => "hello from needlr maui";
}
