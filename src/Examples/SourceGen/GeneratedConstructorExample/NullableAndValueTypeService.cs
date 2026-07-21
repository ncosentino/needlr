using NexusLabs.Needlr.Generators;

namespace GeneratedConstructorExample;

/// <summary>
/// DEMO 5 -- nullable-reference and value-type fields never receive the
/// class-level <see cref="ConstructorNullGuardMode.NonNullableReferences"/>
/// default. Only <c>_repository</c> (a non-nullable reference field) is
/// guarded automatically; <c>_optionalRepository</c> (nullable reference) and
/// <c>_retryCount</c> (value type) are left completely unguarded.
/// <c>_optionalTimeoutSeconds</c> shows that the <c>NotNull</c> built-in guard
/// still works for a <see cref="System.Nullable{T}"/> value type when
/// requested explicitly, because a runtime <see langword="null"/> is possible
/// for <c>int?</c> even though it is never possible for a plain non-nullable
/// <c>int</c>.
/// </summary>
[GenerateConstructor(ConstructorNullGuardMode.NonNullableReferences)]
public partial class NullableAndValueTypeService
{
    private readonly IUserRepository _repository;

    private readonly IUserRepository? _optionalRepository;

    private readonly int _retryCount;

    [ConstructorGuard(ConstructorGuardKind.NotNull)]
    private readonly int? _optionalTimeoutSeconds;

    /// <summary>The non-nullable, automatically guarded repository.</summary>
    public IUserRepository Repository => _repository;

    /// <summary>The nullable, never-guarded repository.</summary>
    public IUserRepository? OptionalRepository => _optionalRepository;

    /// <summary>The value-type field, never guarded regardless of class mode.</summary>
    public int RetryCount => _retryCount;

    /// <summary>The nullable value-type field, guarded only because of its explicit <c>NotNull</c> guard.</summary>
    public int? OptionalTimeoutSeconds => _optionalTimeoutSeconds;
}
