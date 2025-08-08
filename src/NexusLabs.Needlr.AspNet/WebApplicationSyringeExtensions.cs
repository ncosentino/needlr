using NexusLabs.Needlr.Injection;

namespace NexusLabs.Needlr.AspNet;

/// <summary>
/// Extension methods for configuring <see cref="WebApplicationSyringe"/> instances.
/// Provides only web application specific configuration methods.
/// </summary>
/// <remarks>
/// Remember to use base <see cref="Syringe"/> extension methods BEFORE calling 
/// <seealso cref="SyringeAspNetExtensions.ForWebApplication(Syringe)"/>.
/// </remarks>
public static class WebApplicationSyringeExtensions
{
    /// <summary>
    /// Configures the web application syringe to use the specified web application options factory.
    /// </summary>
    /// <param name="syringe">The web application syringe to configure.</param>
    /// <param name="optionsFactory">The factory function for creating web application options.</param>
    /// <returns>A new configured web application syringe instance.</returns>
    public static WebApplicationSyringe UsingOptions(
        this WebApplicationSyringe syringe, 
        Func<CreateWebApplicationOptions> optionsFactory)
    {
        ArgumentNullException.ThrowIfNull(syringe);
        ArgumentNullException.ThrowIfNull(optionsFactory);

        return syringe with { OptionsFactory = optionsFactory };
    }

    /// <summary>
    /// Configures the web application syringe to use the specified web application factory.
    /// </summary>
    /// <param name="syringe">The web application syringe to configure.</param>
    /// <param name="factory">The factory function for creating web application factories.</param>
    /// <returns>A new configured web application syringe instance.</returns>
    public static WebApplicationSyringe UsingWebApplicationFactory(
        this WebApplicationSyringe syringe, 
        Func<IServiceProviderBuilder, IServiceCollectionPopulator, IWebApplicationFactory> factory)
    {
        ArgumentNullException.ThrowIfNull(syringe);
        ArgumentNullException.ThrowIfNull(factory);

        return syringe with { WebApplicationFactoryCreator = factory };
    }
}