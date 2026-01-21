using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace NexusLabs.Needlr.Extensions.Logging;

/// <summary>
/// Extension methods for <see cref="PostBuildServiceCollectionPluginOptions"/> to simplify logger retrieval.
/// </summary>
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
