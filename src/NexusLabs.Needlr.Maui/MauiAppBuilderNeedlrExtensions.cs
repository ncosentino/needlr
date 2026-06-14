using Microsoft.Maui.Hosting;

using NexusLabs.Needlr.Injection;

namespace NexusLabs.Needlr.Maui;

/// <summary>
/// Extension methods that integrate Needlr's discovery into a user-controlled
/// <see cref="MauiAppBuilder"/>.
/// </summary>
/// <remarks>
/// <para>
/// <c>UseNeedlr</c> is a "reverse integration": Needlr adapts to the MAUI builder you already own
/// rather than taking over its creation. Call it from <c>MauiProgram.CreateMauiApp</c> after
/// <c>UseMauiApp&lt;App&gt;()</c>; every Needlr-discovered service is added to
/// <see cref="MauiAppBuilder.Services"/>, and MAUI builds and owns the single resulting provider.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// public static MauiApp CreateMauiApp()
/// {
///     var builder = MauiApp.CreateBuilder();
///     builder
///         .UseMauiApp&lt;App&gt;()
///         .UseNeedlr(syringe =&gt; syringe.UsingSourceGen());
///
///     return builder.Build();
/// }
/// </code>
/// </example>
public static class MauiAppBuilderNeedlrExtensions
{
    /// <summary>
    /// Populates the builder's service collection with the registrations discovered by the
    /// supplied configured syringe.
    /// </summary>
    /// <param name="builder">The MAUI application builder to populate.</param>
    /// <param name="syringe">A configured syringe (for example <c>new Syringe().UsingSourceGen()</c>).</param>
    /// <returns>The same <paramref name="builder"/> instance, to allow chaining.</returns>
    /// <exception cref="System.ArgumentNullException">Thrown when any argument is <see langword="null"/>.</exception>
    public static MauiAppBuilder UseNeedlr(this MauiAppBuilder builder, ConfiguredSyringe syringe)
    {
        System.ArgumentNullException.ThrowIfNull(builder);
        System.ArgumentNullException.ThrowIfNull(syringe);
        return syringe.ForMaui().PopulateInto(builder);
    }

    /// <summary>
    /// Populates the builder's service collection using a syringe configured by
    /// <paramref name="configure"/>. Use this overload to pick the discovery strategy inline, for
    /// example <c>builder.UseNeedlr(s =&gt; s.UsingSourceGen())</c>.
    /// </summary>
    /// <param name="builder">The MAUI application builder to populate.</param>
    /// <param name="configure">
    /// A callback that turns a fresh <see cref="Syringe"/> into a <see cref="ConfiguredSyringe"/>,
    /// typically by calling <c>UsingSourceGen()</c> or <c>UsingReflection()</c>.
    /// </param>
    /// <returns>The same <paramref name="builder"/> instance, to allow chaining.</returns>
    /// <exception cref="System.ArgumentNullException">Thrown when any argument is <see langword="null"/>.</exception>
    public static MauiAppBuilder UseNeedlr(this MauiAppBuilder builder, System.Func<Syringe, ConfiguredSyringe> configure)
    {
        System.ArgumentNullException.ThrowIfNull(builder);
        System.ArgumentNullException.ThrowIfNull(configure);
        return builder.UseNeedlr(configure(new Syringe()));
    }
}
