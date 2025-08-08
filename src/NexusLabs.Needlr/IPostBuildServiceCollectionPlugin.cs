namespace NexusLabs.Needlr;

[DoNotAutoRegister]
public interface IPostBuildServiceCollectionPlugin
{
    /// <summary>
    /// Allows execution of additional configuration after the main service collection has been built.
    /// </summary>
    void Configure(PostBuildServiceCollectionPluginOptions options);
}