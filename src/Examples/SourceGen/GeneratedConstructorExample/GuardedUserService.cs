using NexusLabs.Needlr.Generators;

namespace GeneratedConstructorExample;

/// <summary>
/// DEMO 2 -- <c>[GenerateConstructor(ConstructorNullGuardMode.NonNullableReferences)]</c>
/// automatically guards every eligible non-nullable reference field with
/// <see cref="System.ArgumentNullException"/>. This type is resolved through a
/// real Needlr <c>Syringe</c>-built service provider in <c>Program.cs</c> to
/// prove the generated constructor participates in normal DI activation, not
/// just direct <see langword="new"/> construction.
/// </summary>
[GenerateConstructor(ConstructorNullGuardMode.NonNullableReferences)]
public partial class GuardedUserService
{
    private readonly IUserRepository _repository;

    /// <summary>The repository supplied to the generated constructor.</summary>
    public IUserRepository Repository => _repository;
}
