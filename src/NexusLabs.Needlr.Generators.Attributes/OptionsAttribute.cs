using System;

namespace NexusLabs.Needlr.Generators;

/// <summary>
/// Marks a class as an options/configuration type that should be bound to a configuration section.
/// The source generator will automatically generate the <c>services.Configure&lt;T&gt;()</c> call.
/// </summary>
/// <remarks>
/// <para>
/// When applied to a class, the generator will emit code to bind the class to a configuration section.
/// If no section name is specified, it is inferred from the class name by stripping common suffixes
/// (Options, Settings, Config).
/// </para>
/// <para>
/// All three options interfaces are registered automatically:
/// <list type="bullet">
/// <item><description><c>IOptions&lt;T&gt;</c> - Singleton, no reload</description></item>
/// <item><description><c>IOptionsSnapshot&lt;T&gt;</c> - Scoped, reloads per request</description></item>
/// <item><description><c>IOptionsMonitor&lt;T&gt;</c> - Singleton with change notifications</description></item>
/// </list>
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // Section name inferred as "Database"
/// [Options]
/// public class DatabaseOptions
/// {
///     public string ConnectionString { get; set; } = "";
///     public int CommandTimeout { get; set; } = 30;
/// }
/// 
/// // Explicit section name
/// [Options("MyApp:Database")]
/// public class DbOptions
/// {
///     public string ConnectionString { get; set; } = "";
/// }
/// 
/// // With validation at startup
/// [Options(ValidateOnStart = true)]
/// public class StripeOptions
/// {
///     [Required]
///     public string ApiKey { get; set; } = "";
/// }
/// 
/// // Named options for multiple instances
/// [Options("Databases:Primary", Name = "Primary")]
/// [Options("Databases:Replica", Name = "Replica")]
/// public class ConnectionOptions
/// {
///     public string ConnectionString { get; set; } = "";
///     public bool ReadOnly { get; set; }
/// }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = true)]
public sealed class OptionsAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="OptionsAttribute"/> class
    /// with the section name inferred from the class name.
    /// </summary>
    public OptionsAttribute()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="OptionsAttribute"/> class
    /// with an explicit section name.
    /// </summary>
    /// <param name="sectionName">
    /// The configuration section name to bind to (e.g., "Database" or "MyApp:Database").
    /// </param>
    public OptionsAttribute(string sectionName)
    {
        SectionName = sectionName;
    }

    /// <summary>
    /// Gets the configuration section name to bind to.
    /// If null, the section name is inferred from the class name.
    /// </summary>
    public string? SectionName { get; }

    /// <summary>
    /// Gets or sets the name for named options.
    /// When set, registers as a named option that can be retrieved via
    /// <c>IOptionsSnapshot&lt;T&gt;.Get(name)</c> or <c>IOptionsMonitor&lt;T&gt;.Get(name)</c>.
    /// </summary>
    /// <remarks>
    /// Use this when you need multiple configurations of the same options type,
    /// such as primary and replica database connections.
    /// </remarks>
    public string? Name { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to validate the options at application startup.
    /// When true, validation errors will prevent the application from starting.
    /// </summary>
    /// <remarks>
    /// <para>
    /// When enabled, the generator will emit:
    /// <list type="bullet">
    /// <item><description><c>.ValidateDataAnnotations()</c> - Validates [Required], [Range], etc.</description></item>
    /// <item><description><c>.ValidateOnStart()</c> - Runs validation during startup</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// For custom validation, implement a <c>Validate()</c> method returning <c>IEnumerable&lt;ValidationError&gt;</c>,
    /// or specify an external validator using the <see cref="Validator"/> property.
    /// </para>
    /// </remarks>
    public bool ValidateOnStart { get; set; }

    /// <summary>
    /// Gets or sets the name of the validation method to use.
    /// </summary>
    /// <remarks>
    /// <para>
    /// By default (when null), the generator looks for a method named <c>Validate</c>.
    /// Set this property to use a differently-named method.
    /// </para>
    /// <para>
    /// The method must return <c>IEnumerable&lt;ValidationError&gt;</c> (or <c>IEnumerable&lt;string&gt;</c>
    /// for backward compatibility) and take no parameters (for instance methods) or the options
    /// type as a parameter (for static methods).
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// [Options(ValidateOnStart = true, ValidateMethod = nameof(CheckConfig))]
    /// public class MyOptions
    /// {
    ///     public IEnumerable&lt;ValidationError&gt; CheckConfig()
    ///     {
    ///         if (string.IsNullOrEmpty(Value))
    ///             yield return "Value is required";
    ///     }
    /// }
    /// </code>
    /// </example>
    public string? ValidateMethod { get; set; }

    /// <summary>
    /// Gets or sets the external validator type to use.
    /// </summary>
    /// <remarks>
    /// <para>
    /// When set, validation is delegated to the specified type instead of a method on the options class.
    /// The validator type must implement <c>IOptionsValidator&lt;T&gt;</c> or FluentValidation's
    /// <c>AbstractValidator&lt;T&gt;</c> (if FluentValidation is referenced).
    /// </para>
    /// <para>
    /// Can be combined with <see cref="ValidateMethod"/> to specify a custom method name on the
    /// validator type (default is <c>Validate</c>).
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// [Options(ValidateOnStart = true, Validator = typeof(MyOptionsValidator))]
    /// public class MyOptions { ... }
    /// 
    /// public class MyOptionsValidator : IOptionsValidator&lt;MyOptions&gt;
    /// {
    ///     public IEnumerable&lt;ValidationError&gt; Validate(MyOptions options) { ... }
    /// }
    /// </code>
    /// </example>
    public Type? Validator { get; set; }
}
