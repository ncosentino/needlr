using NexusLabs.Needlr.Injection;

namespace NexusLabs.Needlr.Maui;

/// <summary>
/// Extension methods for transitioning a <see cref="ConfiguredSyringe"/> into .NET MAUI mode.
/// </summary>
/// <example>
/// <code>
/// new Syringe()
///     .UsingSourceGen()
///     .ForMaui()
///     .PopulateInto(builder);
/// </code>
/// </example>
public static class SyringeMauiExtensions
{
    /// <summary>
    /// Transitions the configured syringe into MAUI mode, returning a <see cref="MauiSyringe"/>
    /// that can populate a <c>MauiAppBuilder</c>.
    /// </summary>
    /// <param name="syringe">The configured syringe to transition.</param>
    /// <returns>A new <see cref="MauiSyringe"/> wrapping <paramref name="syringe"/>.</returns>
    /// <exception cref="System.ArgumentNullException">Thrown when <paramref name="syringe"/> is <see langword="null"/>.</exception>
    public static MauiSyringe ForMaui(this ConfiguredSyringe syringe)
    {
        System.ArgumentNullException.ThrowIfNull(syringe);
        return new MauiSyringe(syringe);
    }
}
