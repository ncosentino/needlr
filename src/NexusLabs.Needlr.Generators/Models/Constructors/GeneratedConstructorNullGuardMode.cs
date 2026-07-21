namespace NexusLabs.Needlr.Generators.Models;

/// <summary>
/// Internal mirror of the public <c>ConstructorNullGuardMode</c> enum, used to avoid
/// coupling the discovery layer to the compiled attribute type.
/// </summary>
internal enum GeneratedConstructorNullGuardMode
{
    None = 0,
    NonNullableReferences = 1,
}
