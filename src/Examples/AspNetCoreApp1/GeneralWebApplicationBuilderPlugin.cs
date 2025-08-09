using NexusLabs.Needlr.AspNet;

using System.Net;

/// <summary>
/// This is an example plugin where you are configuring various aspects 
/// of your <see cref="WebApplicationBuilder"/>. You do not need to
/// add any attributes to this class, as it implements the
/// <see cref="IWebApplicationBuilderPlugin"/> interface, and will be
/// automatically registered and invoked by the Needlr framework.
/// </summary>
internal sealed class GeneralWebApplicationBuilderPlugin : IWebApplicationBuilderPlugin
{
    public void Configure(WebApplicationBuilderPluginOptions options)
    {
        var webApplicationBuilder = options.Builder;

        webApplicationBuilder.Services.AddHealthChecks();
        webApplicationBuilder.Services.AddHttpClient();
        webApplicationBuilder.Services.AddHttpsRedirection(options =>
        {
            options.RedirectStatusCode = (int)HttpStatusCode.PermanentRedirect;
            options.HttpsPort = 443;
        });
        webApplicationBuilder.Services.AddHsts(options =>
        {
            options.Preload = true;
            options.IncludeSubDomains = true;
            options.MaxAge = TimeSpan.FromDays(60);
        });
        webApplicationBuilder.Services.AddCors();
        webApplicationBuilder.Services.AddOutputCache();
        webApplicationBuilder.Services.ConfigureHttpJsonOptions(opts =>
        {
            opts.SerializerOptions.PropertyNameCaseInsensitive = true;
            opts.SerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
            opts.SerializerOptions.DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
        });
    }
}