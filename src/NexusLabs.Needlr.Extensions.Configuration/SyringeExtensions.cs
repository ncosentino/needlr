using Microsoft.Extensions.Configuration;

using NexusLabs.Needlr.Injection;

namespace NexusLabs.Needlr.Extensions.Configuration;

public static class SyringeExtensions
{
    /// <summary>
    /// Builds a service provider with an empty <see cref="IConfiguration"/>.
    /// </summary>
    /// <returns>The configured <see cref="IServiceProvider"/>.</returns>
    public static IServiceProvider BuildServiceProvider(
        this Syringe syringe)
    {
        ArgumentNullException.ThrowIfNull(syringe);
        var config = new ConfigurationBuilder().Build();
        return syringe.BuildServiceProvider(config);
    }
}
