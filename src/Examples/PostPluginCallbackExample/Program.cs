using Microsoft.Extensions.Options;

using NexusLabs.Needlr;
using NexusLabs.Needlr.AspNet;
using NexusLabs.Needlr.Injection;

var webApplication = new Syringe()
    .ForWebApplication()
    .UsingOptions(() => CreateWebApplicationOptions.Default
        .UsingStartupConsoleLogger()
        .UsingPostPluginRegistrationCallback(services =>
        {
            // Configure options after plugins have registered their services
            services.Configure<AppSettings>(options =>
            {
                options.Title = "Post-Plugin Callback Example";
                options.Version = "1.0.0";
            });
        })
        .UsingPostPluginRegistrationCallbacks(
            services => services.AddAuthentication(),
            services => services.AddAuthorization())
        .UsingPostPluginRegistrationCallback(services =>
        {
            // Add services that depend on authentication being registered
            services.AddScoped<IUserService, UserService>();
        }))
    .BuildWebApplication();

await webApplication.RunAsync();

public class AppSettings
{
    public string Title { get; set; } = "Default Title";
    public string Version { get; set; } = "0.0.0";
}

public interface IUserService
{
    string GetCurrentUser();
}

[DoNotAutoRegister]
public class UserService : IUserService
{
    public string GetCurrentUser() => "Anonymous";
}

public class ApiPlugin : IWebApplicationPlugin
{
    public void Configure(WebApplicationPluginOptions options)
    {
        options.WebApplication.MapGet("/", (IOptions<AppSettings> settings) =>
        {
            return Results.Ok(new
            {
                settings.Value.Title,
                settings.Value.Version,
                Timestamp = DateTime.UtcNow
            });
        });

        options.WebApplication.MapGet("/user", (IUserService userService) =>
        {
            return Results.Ok(new
            {
                User = userService.GetCurrentUser()
            });
        });
    }
}