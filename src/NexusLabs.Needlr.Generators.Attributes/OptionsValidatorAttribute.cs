// Copyright (c) NexusLabs. All rights reserved.
// Licensed under the MIT License.

namespace NexusLabs.Needlr.Generators;

/// <summary>
/// Marks a method as an options validator.
/// The method must return <c>IEnumerable&lt;string&gt;</c> containing validation error messages.
/// An empty enumerable indicates successful validation.
/// </summary>
/// <remarks>
/// <para>
/// This attribute is used in conjunction with <see cref="OptionsAttribute"/> when
/// <c>ValidateOnStart = true</c>. The source generator discovers methods marked with
/// this attribute and generates an <c>IValidateOptions&lt;T&gt;</c> implementation.
/// </para>
/// <para>
/// The method can be either instance or static. For instance methods, a new instance
/// of the options type is used for validation (the bound instance).
/// </para>
/// <example>
/// <code>
/// [Options(ValidateOnStart = true)]
/// public class StripeOptions
/// {
///     public string ApiKey { get; set; } = "";
///     
///     [OptionsValidator]
///     public IEnumerable&lt;string&gt; Validate()
///     {
///         if (!ApiKey.StartsWith("sk_"))
///             yield return "ApiKey must start with 'sk_'";
///     }
/// }
/// </code>
/// </example>
/// </remarks>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class OptionsValidatorAttribute : Attribute
{
}
