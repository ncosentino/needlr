using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace NexusLabs.Needlr.Extensions.Logging;

public static class PostBuildServiceCollectionPluginOptionsExtensions
{
    public static ILogger GetLogger<T>(this PostBuildServiceCollectionPluginOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return options.Provider.GetRequiredService<ILogger<T>>();
    }

    public static ILogger GetLogger(this PostBuildServiceCollectionPluginOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return options.Provider.GetRequiredService<ILogger>();
    }
}
