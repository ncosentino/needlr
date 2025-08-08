using Microsoft.Extensions.Configuration;

using NexusLabs.Needlr.Injection;

namespace NexusLabs.Needlr.AspNet;

public static class ServiceCollectionBuilderExtensions
{
    public static IServiceProvider Build(
        this IServiceProviderBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.Build(new ConfigurationManager());
    }
}