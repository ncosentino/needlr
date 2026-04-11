using System.ComponentModel.DataAnnotations;

using NexusLabs.Needlr.Generators;

namespace MinimalWebApiSourceGen;

/// <summary>
/// Demonstrates the Needlr <c>[Options]</c> source-generator pattern on the
/// ASP.NET Core web application path. The section name is inferred from the
/// class name: <c>WeatherOptions</c> binds to the <c>"Weather"</c> section of
/// <see cref="Microsoft.Extensions.Configuration.IConfiguration"/>.
/// </summary>
/// <remarks>
/// <para>
/// When the Needlr source generator runs it emits a call equivalent to
/// <c>services.AddOptions&lt;WeatherOptions&gt;().BindConfiguration("Weather")
/// .ValidateDataAnnotations().ValidateOnStart()</c> into a generated
/// <c>TypeRegistry.RegisterOptions</c> method, plus a generated
/// <c>IValidateOptions&lt;WeatherOptions&gt;</c> that checks every
/// <see cref="RequiredAttribute"/> property without reflection.
/// </para>
/// <para>
/// <see cref="NexusLabs.Needlr.AspNet.WebApplicationSyringe.BuildWebApplication"/>
/// is responsible for invoking that generated <c>RegisterOptions</c> method
/// through the source-gen post-plugin callback pipeline. The bug that this
/// example validates the fix for is: prior to the fix, the web path silently
/// skipped the generator call, so <see cref="WeatherProvider"/> would receive
/// an <c>IOptions&lt;WeatherOptions&gt;</c> whose <see cref="Summary"/> was
/// an empty string regardless of what <c>appsettings.json</c> contained.
/// </para>
/// </remarks>
[Options(ValidateOnStart = true)]
public sealed class WeatherOptions
{
    /// <summary>
    /// The current temperature in Celsius. Populated from
    /// <c>Weather:TemperatureCelsius</c>.
    /// </summary>
    [Range(-100, 100)]
    public double TemperatureCelsius { get; set; }

    /// <summary>
    /// A short human-readable description of the weather. Populated from
    /// <c>Weather:Summary</c>. Validation fails at startup if this is
    /// missing, which is how the source-gen <c>ValidateOnStart</c> integration
    /// is exercised.
    /// </summary>
    [Required]
    public string Summary { get; set; } = string.Empty;
}
