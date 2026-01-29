using FluentValidation.Results;

using NexusLabs.Needlr.Generators;

namespace NexusLabs.Needlr.FluentValidation;

/// <summary>
/// Converts FluentValidation <see cref="ValidationFailure"/> instances to Needlr <see cref="ValidationError"/> instances.
/// </summary>
public static class ValidationFailureConverter
{
    /// <summary>
    /// Converts a FluentValidation <see cref="ValidationFailure"/> to a Needlr <see cref="ValidationError"/>.
    /// </summary>
    /// <param name="failure">The FluentValidation failure to convert.</param>
    /// <returns>A Needlr <see cref="ValidationError"/> with the same information.</returns>
    public static ValidationError ToValidationError(this ValidationFailure failure)
    {
        ArgumentNullException.ThrowIfNull(failure);

        return new ValidationError(failure.ErrorMessage)
        {
            PropertyName = failure.PropertyName,
            ErrorCode = failure.ErrorCode,
            Severity = ConvertSeverity(failure.Severity)
        };
    }

    /// <summary>
    /// Converts a collection of FluentValidation failures to Needlr validation errors.
    /// </summary>
    /// <param name="failures">The FluentValidation failures to convert.</param>
    /// <returns>An enumerable of Needlr <see cref="ValidationError"/> instances.</returns>
    public static IEnumerable<ValidationError> ToValidationErrors(this IEnumerable<ValidationFailure> failures)
    {
        ArgumentNullException.ThrowIfNull(failures);

        return failures.Select(f => f.ToValidationError());
    }

    /// <summary>
    /// Converts a FluentValidation <see cref="ValidationResult"/> to a collection of Needlr validation errors.
    /// </summary>
    /// <param name="result">The FluentValidation result to convert.</param>
    /// <returns>An enumerable of Needlr <see cref="ValidationError"/> instances.</returns>
    public static IEnumerable<ValidationError> ToValidationErrors(this ValidationResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        return result.Errors.ToValidationErrors();
    }

    private static ValidationSeverity ConvertSeverity(global::FluentValidation.Severity severity)
    {
        return severity switch
        {
            global::FluentValidation.Severity.Error => ValidationSeverity.Error,
            global::FluentValidation.Severity.Warning => ValidationSeverity.Warning,
            global::FluentValidation.Severity.Info => ValidationSeverity.Info,
            _ => ValidationSeverity.Error
        };
    }
}
