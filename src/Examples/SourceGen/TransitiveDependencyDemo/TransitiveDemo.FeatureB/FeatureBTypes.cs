using Microsoft.Extensions.DependencyInjection;

using NexusLabs.Needlr;

using TransitiveDemo.FeatureA;

namespace TransitiveDemo.FeatureB;

/// <summary>
/// A feature service that depends on ICoreLogger from FeatureA.
/// </summary>
public interface IFeatureBService
{
    void DoWork();
}

internal sealed class FeatureBService(ICoreLogger logger) : IFeatureBService
{
    public void DoWork()
    {
        logger.Log("FeatureB is doing work!");
    }
}

/// <summary>
/// This plugin depends on ICoreLogger being already registered by FeatureA's plugin.
/// It demonstrates the cross-plugin dependency scenario.
/// </summary>
internal sealed class FeatureBPlugin : IPostBuildServiceCollectionPlugin
{
    public void Configure(PostBuildServiceCollectionPluginOptions options)
    {
        Console.WriteLine("[FeatureB] Post-build plugin executing");
        
        // This will fail if FeatureA's plugin didn't run and register ICoreLogger!
        var logger = options.Provider.GetRequiredService<ICoreLogger>();
        logger.Log("FeatureB plugin initialized successfully!");
    }
}
