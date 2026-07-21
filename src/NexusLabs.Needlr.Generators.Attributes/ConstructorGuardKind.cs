namespace NexusLabs.Needlr.Generators;

/// <summary>
/// Identifies a built-in constructor guard clause that
/// <see cref="ConstructorGuardAttribute"/> can request for a field participating in
/// generated constructor generation.
/// </summary>
/// <remarks>
/// <para>
/// Built-in guards emit the equivalent standard .NET guard-clause call (for example
/// <c>ArgumentNullException.ThrowIfNull(object?, string?)</c>) directly
/// in the generated constructor, so the reported exception type and
/// <c>ParamName</c> exactly match what the same call would produce if hand-written.
/// </para>
/// <para>
/// Applicability of a given kind to a given field type is validated by an analyzer;
/// for example <see cref="NotNullOrWhiteSpace"/> only applies to <see cref="string"/>
/// -compatible fields.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// public partial class TenantService
/// {
///     [ConstructorGuard(ConstructorGuardKind.NotNullOrWhiteSpace)]
///     private readonly string _tenantName;
/// }
///
/// // Generated:
/// // public TenantService(string tenantName)
/// // {
/// //     ArgumentException.ThrowIfNullOrWhiteSpace(tenantName);
/// //     _tenantName = tenantName;
/// // }
/// </code>
/// </example>
public enum ConstructorGuardKind
{
    /// <summary>
    /// No guard. When applied explicitly, suppresses an applicable class-level
    /// automatic guard default for this field without affecting other guards.
    /// </summary>
    None = 0,

    /// <summary>
    /// Throws <see cref="System.ArgumentNullException"/> when the value is
    /// <see langword="null"/>.
    /// </summary>
    NotNull = 1,

    /// <summary>
    /// Throws <see cref="System.ArgumentNullException"/> when the value is
    /// <see langword="null"/>, or <see cref="System.ArgumentException"/> when it is an
    /// empty string.
    /// </summary>
    NotNullOrEmpty = 2,

    /// <summary>
    /// Throws <see cref="System.ArgumentNullException"/> when the value is
    /// <see langword="null"/>, or <see cref="System.ArgumentException"/> when it is
    /// empty or consists only of white-space characters.
    /// </summary>
    NotNullOrWhiteSpace = 3,
}
