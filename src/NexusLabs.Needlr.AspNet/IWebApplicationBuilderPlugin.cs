namespace NexusLabs.Needlr.AspNet;

[DoNotAutoRegister]
[DoNotInject]
public interface IWebApplicationBuilderPlugin
{
    void Configure(WebApplicationBuilderPluginOptions options);
}
