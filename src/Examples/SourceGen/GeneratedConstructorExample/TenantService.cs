using NexusLabs.Needlr.Generators;

namespace GeneratedConstructorExample;

/// <summary>
/// DEMO 3 -- field-triggered implicit generation. This class carries no
/// <c>[GenerateConstructor]</c> attribute at all. The positive
/// <c>[ConstructorGuard(ConstructorGuardKind.NotNullOrWhiteSpace)]</c> on
/// <c>_tenantName</c> alone triggers constructor generation at the default
/// <see cref="ConstructorNullGuardMode.None"/>: every eligible field becomes a
/// constructor parameter, but only <c>_tenantName</c> is guarded -- the
/// unannotated <c>_repository</c> field stays unguarded and accepts
/// <see langword="null"/>.
/// </summary>
public partial class TenantService
{
    private readonly IUserRepository _repository;

    [ConstructorGuard(ConstructorGuardKind.NotNullOrWhiteSpace)]
    private readonly string _tenantName;

    /// <summary>The repository supplied to the generated constructor (unguarded).</summary>
    public IUserRepository Repository => _repository;

    /// <summary>The tenant name supplied to the generated constructor (guarded).</summary>
    public string TenantName => _tenantName;
}
