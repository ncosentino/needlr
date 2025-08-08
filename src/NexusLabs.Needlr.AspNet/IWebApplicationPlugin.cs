namespace NexusLabs.Needlr.AspNet;

[DoNotAutoRegister]
public interface IWebApplicationPlugin
{
    void Configure(WebApplicationPluginOptions options);
}
