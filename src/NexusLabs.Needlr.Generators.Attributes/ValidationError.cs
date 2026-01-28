// Copyright (c) NexusLabs. All rights reserved.
// Licensed under the MIT License.

namespace NexusLabs.Needlr.Generators;

/// <summary>
/// Represents a validation error with optional structured information.
/// </summary>
/// <remarks>
/// <para>
/// This class provides rich validation error information including the property name,
/// error code, and severity level. For simple cases, strings can be implicitly converted
/// to <see cref="ValidationError"/> via the implicit operator.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // Simple: just yield a string (implicit conversion)
/// yield return "Name is required";
/// 
/// // Rich: provide structured information
/// yield return new ValidationError("API key format is invalid")
/// {
///     PropertyName = nameof(ApiKey),
///     ErrorCode = "API_KEY_FORMAT",
///     Severity = ValidationSeverity.Error
/// };
/// 
/// // Warning (won't fail startup)
/// yield return new ValidationError("Timeout is unusually high")
/// {
///     PropertyName = nameof(Timeout),
///     Severity = ValidationSeverity.Warning
/// };
/// </code>
/// </example>
public sealed class ValidationError
{
    /// <summary>
    /// Initializes a new instance of <see cref="ValidationError"/> with the specified message.
    /// </summary>
    /// <param name="message">The error message.</param>
    public ValidationError(string message)
    {
        Message = message ?? throw new System.ArgumentNullException(nameof(message));
    }

    /// <summary>
    /// Gets the error message.
    /// </summary>
    public string Message { get; }

    /// <summary>
    /// Gets or sets the name of the property that failed validation, if applicable.
    /// </summary>
    public string? PropertyName { get; set; }

    /// <summary>
    /// Gets or sets an error code for programmatic handling or localization.
    /// </summary>
    public string? ErrorCode { get; set; }

    /// <summary>
    /// Gets or sets the severity of the validation error.
    /// Only <see cref="ValidationSeverity.Error"/> will cause startup to fail.
    /// </summary>
    public ValidationSeverity Severity { get; set; } = ValidationSeverity.Error;

    /// <summary>
    /// Implicitly converts a string to a <see cref="ValidationError"/>.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <returns>A new <see cref="ValidationError"/> with the specified message.</returns>
    public static implicit operator ValidationError(string message)
        => new ValidationError(message);

    /// <summary>
    /// Returns the error message, optionally prefixed with the property name.
    /// </summary>
    public override string ToString()
        => PropertyName != null ? $"{PropertyName}: {Message}" : Message;

    /// <summary>
    /// Determines whether the specified object is equal to the current object.
    /// </summary>
    public override bool Equals(object? obj)
    {
        if (obj is ValidationError other)
        {
            return Message == other.Message &&
                   PropertyName == other.PropertyName &&
                   ErrorCode == other.ErrorCode &&
                   Severity == other.Severity;
        }
        return false;
    }

    /// <summary>
    /// Returns the hash code for this validation error.
    /// </summary>
    public override int GetHashCode()
    {
        unchecked
        {
            var hash = 17;
            hash = hash * 23 + (Message?.GetHashCode() ?? 0);
            hash = hash * 23 + (PropertyName?.GetHashCode() ?? 0);
            hash = hash * 23 + (ErrorCode?.GetHashCode() ?? 0);
            hash = hash * 23 + Severity.GetHashCode();
            return hash;
        }
    }
}

/// <summary>
/// Specifies the severity of a validation error.
/// </summary>
public enum ValidationSeverity
{
    /// <summary>
    /// An error that will cause validation to fail and prevent startup.
    /// </summary>
    Error,

    /// <summary>
    /// A warning that will be logged but won't prevent startup.
    /// </summary>
    Warning,

    /// <summary>
    /// An informational message that will be logged.
    /// </summary>
    Info
}
