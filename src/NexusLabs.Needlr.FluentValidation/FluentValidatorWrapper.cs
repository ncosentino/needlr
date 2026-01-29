using FluentValidation;

using NexusLabs.Needlr.Generators;

namespace NexusLabs.Needlr.FluentValidation;

/// <summary>
/// Wraps a FluentValidation <see cref="IValidator{T}"/> to implement Needlr's
/// <see cref="IOptionsValidator{T}"/> interface.
/// </summary>
/// <typeparam name="TOptions">The options type being validated.</typeparam>
/// <typeparam name="TValidator">The FluentValidation validator type.</typeparam>
/// <remarks>
/// <para>
/// This wrapper allows FluentValidation validators to be used with the
/// <c>[Options(Validator = typeof(...))]</c> attribute by implementing
/// the <see cref="IOptionsValidator{T}"/> interface.
/// </para>
/// <para>
/// Example usage:
/// <code>
/// // The FluentValidation validator
/// public class DatabaseOptionsValidator : AbstractValidator&lt;DatabaseOptions&gt;
/// {
///     public DatabaseOptionsValidator()
///     {
///         RuleFor(x => x.ConnectionString).NotEmpty();
///     }
/// }
/// 
/// // Wrap it for use with Needlr
/// public class DatabaseOptionsNeedlrValidator 
///     : FluentValidatorWrapper&lt;DatabaseOptions, DatabaseOptionsValidator&gt;
/// {
///     public DatabaseOptionsNeedlrValidator() : base(new DatabaseOptionsValidator()) { }
/// }
/// 
/// // Use with [Options]
/// [Options(ValidateOnStart = true, Validator = typeof(DatabaseOptionsNeedlrValidator))]
/// public class DatabaseOptions { ... }
/// </code>
/// </para>
/// </remarks>
public class FluentValidatorWrapper<TOptions, TValidator> : IOptionsValidator<TOptions>
    where TOptions : class
    where TValidator : IValidator<TOptions>
{
    private readonly TValidator _validator;

    /// <summary>
    /// Initializes a new instance wrapping the specified FluentValidation validator.
    /// </summary>
    /// <param name="validator">The FluentValidation validator to wrap.</param>
    public FluentValidatorWrapper(TValidator validator)
    {
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
    }

    /// <summary>
    /// Validates the specified options instance using FluentValidation.
    /// </summary>
    /// <param name="options">The options instance to validate.</param>
    /// <returns>An enumerable of validation errors.</returns>
    public IEnumerable<ValidationError> Validate(TOptions options)
    {
        var result = _validator.Validate(options);
        return result.ToValidationErrors();
    }
}

/// <summary>
/// A convenience base class for creating Needlr-compatible validators from FluentValidation validators.
/// </summary>
/// <typeparam name="TOptions">The options type being validated.</typeparam>
/// <remarks>
/// <para>
/// Inherit from this class and define your validation rules in the constructor.
/// The resulting class can be used directly with <c>[Options(Validator = typeof(...))]</c>.
/// </para>
/// <para>
/// Example:
/// <code>
/// [Options(ValidateOnStart = true, Validator = typeof(DatabaseOptionsValidator))]
/// public class DatabaseOptions
/// {
///     public string ConnectionString { get; set; } = "";
///     public int MaxConnections { get; set; } = 100;
/// }
/// 
/// public class DatabaseOptionsValidator : FluentOptionsValidator&lt;DatabaseOptions&gt;
/// {
///     public DatabaseOptionsValidator()
///     {
///         RuleFor(x => x.ConnectionString)
///             .NotEmpty()
///             .WithMessage("Connection string is required");
///         
///         RuleFor(x => x.MaxConnections)
///             .InclusiveBetween(1, 1000)
///             .WithMessage("Max connections must be between 1 and 1000");
///     }
/// }
/// </code>
/// </para>
/// </remarks>
public abstract class FluentOptionsValidator<TOptions> : AbstractValidator<TOptions>, IOptionsValidator<TOptions>
    where TOptions : class
{
    /// <summary>
    /// Validates the specified options instance.
    /// </summary>
    /// <param name="options">The options instance to validate.</param>
    /// <returns>An enumerable of validation errors.</returns>
    IEnumerable<ValidationError> IOptionsValidator<TOptions>.Validate(TOptions options)
    {
        var result = base.Validate(options);
        return result.ToValidationErrors();
    }
}
