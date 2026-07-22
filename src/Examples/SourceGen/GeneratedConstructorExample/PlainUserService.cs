using NexusLabs.Needlr.Generators;

namespace GeneratedConstructorExample;

/// <summary>
/// DEMO 1 -- bare <c>[GenerateConstructor]</c> with no guard mode. This is
/// equivalent to <c>[GenerateConstructor(ConstructorNullGuardMode.None)]</c>:
/// the generator emits a plain assignment for <c>_repository</c> with no
/// runtime null guard, even though the field is a non-nullable reference type.
/// </summary>
[GenerateConstructor]
public partial class PlainUserService
{
    private readonly IUserRepository _repository;

    /// <summary>The repository supplied to the generated constructor.</summary>
    public IUserRepository Repository => _repository;
}
