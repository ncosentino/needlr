namespace NexusLabs.Needlr.AspNet;

/// <summary>
/// Defines a plugin that configures the <see cref="Microsoft.AspNetCore.Builder.WebApplicationBuilder"/> before the application is built.
/// Implement this interface to add services, configure logging, or modify the builder during application startup.
/// </summary>
/// <remarks>
/// <para>
/// Do <b>not</b> apply <see cref="NexusLabs.Needlr.DoNotAutoRegisterAttribute"/> directly to an implementing class.
/// This interface already carries the attribute to prevent DI registration of the interface itself;
/// adding it to the class too is redundant and was historically a silent bug that suppressed plugin
/// discovery. Analyzer NDLRCOR016 will warn you if you do this.
/// </para>
/// </remarks>
[DoNotAutoRegister]
[DoNotInject]
public interface IWebApplicationBuilderPlugin
{
    void Configure(WebApplicationBuilderPluginOptions options);
}
