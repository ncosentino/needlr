using Microsoft.Extensions.Configuration;

using NexusLabs.Needlr.Injection;

namespace NexusLabs.Needlr.AspNet;

/// <summary>
/// Extension methods for <see cref="IServiceProviderBuilder"/> to build service providers without explicit configuration.
/// </summary>
public static class ServiceCollectionBuilderExtensions
{
    /// <summary>
    /// Builds a service provider using a default empty configuration.
    /// </summary>
    /// <param name="builder">The service provider builder.</param>
    /// <returns>The built service provider.</returns>
    public static IServiceProvider Build(
        this IServiceProviderBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.Build(new ConfigurationManager());
    }
}