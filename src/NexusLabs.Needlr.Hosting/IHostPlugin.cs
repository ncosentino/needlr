namespace NexusLabs.Needlr.Hosting;

/// <summary>
/// Plugin that configures the <see cref="Microsoft.Extensions.Hosting.IHost"/> after it's built 
/// but before it runs. Analogous to <c>IWebApplicationPlugin</c> for web apps.
/// </summary>
[DoNotAutoRegister]
[DoNotInject]
public interface IHostPlugin
{
    void Configure(HostPluginOptions options);
}
