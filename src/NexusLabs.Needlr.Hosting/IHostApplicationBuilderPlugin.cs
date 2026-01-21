namespace NexusLabs.Needlr.Hosting;

/// <summary>
/// Plugin that configures the <see cref="Microsoft.Extensions.Hosting.HostApplicationBuilder"/> 
/// before the host is built. Analogous to <c>IWebApplicationBuilderPlugin</c> for web apps.
/// </summary>
[DoNotAutoRegister]
[DoNotInject]
public interface IHostApplicationBuilderPlugin
{
    void Configure(HostApplicationBuilderPluginOptions options);
}
