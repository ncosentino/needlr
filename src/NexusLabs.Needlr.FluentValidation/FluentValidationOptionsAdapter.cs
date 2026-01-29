using FluentValidation;
using FluentValidation.Results;

using Microsoft.Extensions.Options;

using NexusLabs.Needlr.Generators;

namespace NexusLabs.Needlr.FluentValidation;

/// <summary>
/// Adapts a FluentValidation <see cref="IValidator{T}"/> to work as an
/// <see cref="IValidateOptions{TOptions}"/> for Microsoft.Extensions.Options.
/// </summary>
/// <typeparam name="TOptions">The options type being validated.</typeparam>
/// <remarks>
/// <para>
/// This adapter allows FluentValidation validators to be used seamlessly with
/// Needlr's <c>[Options(ValidateOnStart = true)]</c> attribute. The adapter
/// translates FluentValidation's <see cref="ValidationResult"/> into the
/// <see cref="ValidateOptionsResult"/> expected by the options framework.
/// </para>
/// <para>
/// Usage with Needlr source generation:
/// <code>
/// [Options("Database", ValidateOnStart = true, Validator = typeof(DatabaseOptionsValidator))]
/// public class DatabaseOptions { ... }
/// 
/// public class DatabaseOptionsValidator : AbstractValidator&lt;DatabaseOptions&gt;
/// {
///     public DatabaseOptionsValidator()
///     {
///         RuleFor(x => x.ConnectionString).NotEmpty();
///     }
/// }
/// </code>
/// </para>
/// <para>
/// Register the adapter in your DI container:
/// <code>
/// services.AddFluentValidationOptionsAdapter&lt;DatabaseOptions, DatabaseOptionsValidator&gt;();
/// </code>
/// </para>
/// </remarks>
public sealed class FluentValidationOptionsAdapter<TOptions> : IValidateOptions<TOptions>
    where TOptions : class
{
    private readonly IValidator<TOptions> _validator;
    private readonly string? _name;

    /// <summary>
    /// Creates a new adapter for the specified FluentValidation validator.
    /// </summary>
    /// <param name="validator">The FluentValidation validator to adapt.</param>
    /// <param name="name">Optional name for named options validation.</param>
    public FluentValidationOptionsAdapter(IValidator<TOptions> validator, string? name = null)
    {
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        _name = name;
    }

    /// <summary>
    /// Validates the specified options instance.
    /// </summary>
    /// <param name="name">The name of the options instance being validated.</param>
    /// <param name="options">The options instance to validate.</param>
    /// <returns>A <see cref="ValidateOptionsResult"/> indicating success or failure.</returns>
    public ValidateOptionsResult Validate(string? name, TOptions options)
    {
        // If this adapter is for a specific named options, skip validation for other names
        if (_name != null && _name != name)
        {
            return ValidateOptionsResult.Skip;
        }

        if (options == null)
        {
            return ValidateOptionsResult.Fail($"Options of type {typeof(TOptions).Name} cannot be null.");
        }

        var result = _validator.Validate(options);

        if (result.IsValid)
        {
            return ValidateOptionsResult.Success;
        }

        var errors = result.Errors
            .Where(f => f.Severity == Severity.Error)
            .Select(FormatError)
            .ToList();

        return errors.Count > 0
            ? ValidateOptionsResult.Fail(errors)
            : ValidateOptionsResult.Success;
    }

    private static string FormatError(ValidationFailure failure)
    {
        if (string.IsNullOrEmpty(failure.PropertyName))
        {
            return failure.ErrorMessage;
        }

        return $"{failure.PropertyName}: {failure.ErrorMessage}";
    }
}
