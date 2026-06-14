namespace NeedlrMauiExampleApp;

/// <summary>
/// Default <see cref="IGreetingService"/>. No manual registration is required — Needlr's source
/// generator discovers this type on the MAUI head and registers it as <see cref="IGreetingService"/>.
/// </summary>
public sealed class GreetingService : IGreetingService
{
    /// <inheritdoc />
    public string Greet() => "Injected by Needlr source-gen DI \u2713";
}
