namespace GeneratedConstructorExample;

/// <summary>
/// A simple injectable dependency used throughout this example to demonstrate
/// generated-constructor parameter binding and null-guard behavior.
/// </summary>
public interface IUserRepository
{
    /// <summary>Returns a short, human-readable description of this repository.</summary>
    string Describe();
}
