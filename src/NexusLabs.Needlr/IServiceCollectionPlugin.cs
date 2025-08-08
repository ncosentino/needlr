namespace NexusLabs.Needlr;

/// <summary>
/// Defines a plugin that can configure the <see cref="IServiceCollection"/> for dependency injection.
/// </summary>
[DoNotAutoRegister]
public interface IServiceCollectionPlugin
{
    /// <summary>
    /// Configures the <see cref="IServiceCollection"/> instance.
    /// </summary>
    void Configure(ServiceCollectionPluginOptions options);
}