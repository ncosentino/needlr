namespace NexusLabs.Needlr.Generators.Models;

/// <summary>
/// Internal mirror of the public <c>ConstructorGuardKind</c> enum, extended with
/// <see cref="Custom"/> to represent a resolved custom guard type/method call.
/// </summary>
internal enum GeneratedConstructorGuardKind
{
    None = 0,
    NotNull = 1,
    NotNullOrEmpty = 2,
    NotNullOrWhiteSpace = 3,

    /// <summary>
    /// A custom guard resolved to a specific guard type and static validation method.
    /// </summary>
    Custom = 4,
}
