namespace NexusLabs.Needlr.AspNet;

[DoNotAutoRegister]
public interface IWebApplicationBuilderPlugin
{
    void Configure(WebApplicationBuilderPluginOptions options);
}
