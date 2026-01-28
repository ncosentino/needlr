// Copyright (c) NexusLabs. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;

namespace NexusLabs.Needlr.Generators;

/// <summary>
/// Interface for validating options types.
/// </summary>
/// <typeparam name="T">The options type to validate.</typeparam>
/// <remarks>
/// <para>
/// Implement this interface to create an external validator for an options type.
/// The validator is specified using the <c>Validator</c> property on the
/// <see cref="OptionsAttribute"/>.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// [Options(ValidateOnStart = true, Validator = typeof(StripeOptionsValidator))]
/// public class StripeOptions
/// {
///     public string ApiKey { get; set; } = "";
/// }
/// 
/// public class StripeOptionsValidator : IOptionsValidator&lt;StripeOptions&gt;
/// {
///     public IEnumerable&lt;ValidationError&gt; Validate(StripeOptions options)
///     {
///         if (!options.ApiKey.StartsWith("sk_"))
///             yield return new ValidationError
///             {
///                 Message = "API key must start with 'sk_'",
///                 PropertyName = nameof(StripeOptions.ApiKey)
///             };
///     }
/// }
/// </code>
/// </example>
public interface IOptionsValidator<in T> where T : class
{
    /// <summary>
    /// Validates the specified options instance.
    /// </summary>
    /// <param name="options">The options instance to validate.</param>
    /// <returns>
    /// An enumerable of validation errors. Return an empty enumerable if validation succeeds.
    /// </returns>
    IEnumerable<ValidationError> Validate(T options);
}
