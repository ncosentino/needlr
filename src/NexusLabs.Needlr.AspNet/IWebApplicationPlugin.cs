namespace NexusLabs.Needlr.AspNet;

[DoNotAutoRegister]
[DoNotInject]
public interface IWebApplicationPlugin
{
    void Configure(WebApplicationPluginOptions options);
}
