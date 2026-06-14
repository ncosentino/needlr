namespace NeedlrMauiExampleApp;

/// <summary>
/// A demo service that Needlr discovers and registers automatically when the source generator
/// scans this MAUI head project. It is injected into <see cref="MainPage"/>.
/// </summary>
public interface IGreetingService
{
    /// <summary>Returns a greeting to display on the main page.</summary>
    string Greet();
}
