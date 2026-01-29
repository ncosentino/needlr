using System;

namespace NexusLabs.Needlr.Generators;

/// <summary>
/// Marks an assembly as providing a validator base type that the Needlr analyzer should recognize.
/// When a type used with <c>[Options(Validator = typeof(...))]</c> inherits from the specified base type,
/// the analyzer will not report NDLRGEN014 (validator missing interface).
/// </summary>
/// <remarks>
/// <para>
/// This enables integration packages (like FluentValidation adapters) to teach the core analyzer
/// about their validator types without the core needing direct knowledge of those packages.
/// </para>
/// <para>
/// Example usage in an extension package:
/// <code>
/// [assembly: ValidatorProvider("FluentValidation.AbstractValidator`1")]
/// </code>
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
public sealed class ValidatorProviderAttribute : Attribute
{
    /// <summary>
    /// The metadata name of the base validator type.
    /// Use backtick notation for generic types: "FluentValidation.AbstractValidator`1"
    /// </summary>
    public string BaseTypeName { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ValidatorProviderAttribute"/> class.
    /// </summary>
    /// <param name="baseTypeName">The fully-qualified metadata name of the base validator type.</param>
    public ValidatorProviderAttribute(string baseTypeName)
    {
        BaseTypeName = baseTypeName ?? throw new ArgumentNullException(nameof(baseTypeName));
    }
}
