using System;

namespace NexusLabs.Needlr.Generators;

/// <summary>
/// Generates a public constructor for a partial class from its eligible private
/// instance <see langword="readonly"/> fields, eliminating hand-written constructor,
/// field-assignment, and guard-clause boilerplate.
/// </summary>
/// <remarks>
/// <para>
/// The generator produces exactly one public constructor whose parameters are derived,
/// in declaration order, from every eligible field: a private, instance,
/// <see langword="readonly"/> field without an initializer, unless the field carries
/// <see cref="ConstructorIgnoreAttribute"/>. Field names using the <c>_camelCase</c>
/// convention are mapped to <c>camelCase</c> parameter names.
/// </para>
/// <para>
/// This attribute never introduces optional parameters or overloads on the generated
/// constructor itself. Use an explicit hand-written constructor overload, or a
/// get-only property with its own initialization logic, when optional construction
/// paths are required.
/// </para>
/// <para>
/// The containing class must be declared <see langword="partial"/> and must not
/// declare its own instance constructor, because Roslyn source generators can only add
/// new members to a partial type -- they cannot rewrite or inject statements into a
/// user-authored constructor. The containing type must also not be nested. It may
/// derive from <see cref="object"/> directly, or from any base type that itself has an
/// accessible (public or protected) parameterless constructor -- including the common
/// case of a base type with no explicit constructors at all -- since the generated
/// constructor relies on the implicit <c>: base()</c> call. This supports common
/// framework base types such as <c>BackgroundService</c>. A base type that requires
/// constructor arguments is unsupported: no source is emitted, and an analyzer reports
/// the requirement at compile time instead.
/// </para>
/// <para>
/// A field-level positive guard attribute -- a built-in or custom
/// <see cref="ConstructorGuardAttribute"/>, or a custom alias attribute defined with
/// <see cref="ConstructorGuardDefinitionAttribute"/> -- also enables constructor
/// generation with <see cref="ConstructorNullGuardMode.None"/>, even when this
/// attribute is not applied to the class.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// [GenerateConstructor]
/// public partial class UserService
/// {
///     private readonly IRepository _repository;
/// }
///
/// // Generated:
/// // public UserService(IRepository repository)
/// // {
/// //     _repository = repository;
/// // }
/// </code>
/// <code>
/// [GenerateConstructor]
/// public partial class MyWorker : BackgroundService
/// {
///     private readonly IRepository _repository;
///
///     protected override Task ExecuteAsync(CancellationToken stoppingToken)
///     {
///         return Task.CompletedTask;
///     }
/// }
///
/// // BackgroundService has an accessible parameterless constructor, so the generated
/// // constructor relies on the implicit base() call:
/// // public MyWorker(IRepository repository)
/// // {
/// //     _repository = repository;
/// // }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class GenerateConstructorAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GenerateConstructorAttribute"/>
    /// class with <see cref="ConstructorNullGuardMode.None"/>. No automatic null
    /// guards are emitted.
    /// </summary>
    public GenerateConstructorAttribute()
        : this(ConstructorNullGuardMode.None)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="GenerateConstructorAttribute"/>
    /// class with an explicit null-guard mode.
    /// </summary>
    /// <param name="mode">
    /// Controls whether the generator automatically emits null guards for eligible
    /// non-nullable reference-type fields.
    /// </param>
    public GenerateConstructorAttribute(ConstructorNullGuardMode mode)
    {
        Mode = mode;
    }

    /// <summary>
    /// Gets the configured null-guard mode for the generated constructor.
    /// </summary>
    public ConstructorNullGuardMode Mode { get; }
}
