namespace NexusLabs.Needlr;

/// <summary>
/// Defines a plugin that executes after the service provider has been built.
/// Implement this interface to perform configuration that requires access to the fully built service provider.
/// </summary>
[DoNotAutoRegister]
[DoNotInject]
public interface IPostBuildServiceCollectionPlugin
{
    /// <summary>
    /// Allows execution of additional configuration after the main service collection has been built.
    /// </summary>
    void Configure(PostBuildServiceCollectionPluginOptions options);
}