using Microsoft.Extensions.Hosting;

namespace NexusLabs.Needlr.Hosting;

/// <summary>
/// Factory interface for creating <see cref="IHost"/> instances with Needlr configuration.
/// </summary>
public interface IHostFactory
{
    /// <summary>
    /// Creates an <see cref="IHost"/> using the specified options and builder callback.
    /// </summary>
    /// <param name="options">The options for creating the host.</param>
    /// <param name="createHostApplicationBuilderCallback">The callback that creates and configures the <see cref="HostApplicationBuilder"/>.</param>
    /// <returns>The configured <see cref="IHost"/>.</returns>
    IHost Create(
        CreateHostOptions options,
        Func<HostApplicationBuilder> createHostApplicationBuilderCallback);
}
