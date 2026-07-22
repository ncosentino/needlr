using System;

namespace NexusLabs.Needlr.Generators;

/// <summary>
/// Excludes a field from participating in generated constructor generation.
/// </summary>
/// <remarks>
/// <para>
/// This attribute is purely an exclusion modifier. Applying it by itself, without
/// <see cref="GenerateConstructorAttribute"/> on the class or a positive guard
/// attribute on another field, has no effect because there is no constructor
/// generation to exclude the field from.
/// </para>
/// <para>
/// Use this attribute to keep a private <see langword="readonly"/> field that would
/// otherwise be eligible for generation, but is deliberately populated by something
/// other than the generated constructor -- for example, a field a reflection-based
/// deserializer assigns directly, bypassing the constructor entirely.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// [GenerateConstructor]
/// public partial class CacheEntry
/// {
///     private readonly IRepository _repository;
///
///     // Populated by a reflection-based deserializer after construction, never
///     // supplied by a caller -- excluded so it never becomes a required parameter.
///     [ConstructorIgnore]
///     private readonly string? _serializedPayload;
/// }
///
/// // Generated:
/// // public CacheEntry(IRepository repository)
/// // {
/// //     _repository = repository;
/// // }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Field, Inherited = false, AllowMultiple = false)]
public sealed class ConstructorIgnoreAttribute : Attribute
{
}
