using System;

namespace NexusLabs.Needlr.Generators;

/// <summary>
/// Controls whether <see cref="GenerateConstructorAttribute"/> automatically emits
/// null guards for eligible constructor parameters.
/// </summary>
/// <remarks>
/// <para>
/// This mode only ever affects reference-type fields. Value-type fields (including
/// nullable value types) never receive an automatic null guard, and nullable
/// reference-type fields are always excluded from the automatic default because a
/// nullable annotation is an explicit declaration that <see langword="null"/> is a
/// valid value.
/// </para>
/// <para>
/// A field can opt out of the class-level default for itself by applying
/// <c>[ConstructorGuard(ConstructorGuardKind.None)]</c>, regardless of the mode
/// configured here.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// [GenerateConstructor(ConstructorNullGuardMode.NonNullableReferences)]
/// public partial class UserService
/// {
///     private readonly IRepository _repository;
/// }
///
/// // Generated:
/// // public UserService(IRepository repository)
/// // {
/// //     ArgumentNullException.ThrowIfNull(repository);
/// //     _repository = repository;
/// // }
/// </code>
/// </example>
public enum ConstructorNullGuardMode
{
    /// <summary>
    /// No automatic null guards are emitted. Field assignments are generated as-is.
    /// This is the default used by the parameterless <see cref="GenerateConstructorAttribute"/>
    /// constructor.
    /// </summary>
    None = 0,

    /// <summary>
    /// Automatically emits a null guard for every eligible non-nullable reference-type
    /// field. Nullable reference-type fields and value-type fields are left unguarded.
    /// </summary>
    NonNullableReferences = 1,
}
