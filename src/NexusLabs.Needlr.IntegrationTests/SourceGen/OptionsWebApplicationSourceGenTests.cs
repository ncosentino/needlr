using System.Collections.Generic;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using NexusLabs.Needlr.AspNet;
using NexusLabs.Needlr.Injection;
using NexusLabs.Needlr.Injection.SourceGen;

using Xunit;

namespace NexusLabs.Needlr.IntegrationTests.SourceGen;

/// <summary>
/// Integration tests verifying that [Options]-decorated classes bind
/// correctly when the service provider is built via the ASP.NET Core
/// WebApplication path (<see cref="WebApplicationSyringe.BuildWebApplication"/>).
/// </summary>
/// <remarks>
/// <para>
/// The console path (<see cref="ConfiguredSyringe.BuildServiceProvider(IConfiguration)"/>)
/// invokes the source-generated <c>TypeRegistry.RegisterOptions</c> method via
/// <see cref="SourceGenRegistry.TryGetOptionsRegistrar"/>. Prior to the fix that
/// accompanies this test file, the web path did not — it called
/// <c>BaseSyringe.GetPostPluginRegistrationCallbacks()</c> which only returns
/// user-registered callbacks, silently dropping the source-gen options registrar
/// and every <c>AddOptions&lt;T&gt;().BindConfiguration(...)</c> it emits.
/// </para>
/// <para>
/// These tests use the shared <c>TestDatabaseOptions</c>, <c>CustomSectionOptions</c>,
/// <c>EndpointOptions</c>, <c>ValidatedTestOptions</c>, and related fixtures declared in
/// <see cref="OptionsSourceGenTests"/>. They re-run the critical console-path
/// scenarios through the web path so console and web stay at parity.
/// </para>
/// </remarks>
public sealed class OptionsWebApplicationSourceGenTests
{
    private static IServiceProvider BuildWebAppServices(
        Dictionary<string, string?> inMemoryConfig)
    {
        var webApplication = new Syringe()
            .UsingGeneratedComponents(
                NexusLabs.Needlr.IntegrationTests.Generated.TypeRegistry.GetInjectableTypes,
                NexusLabs.Needlr.IntegrationTests.Generated.TypeRegistry.GetPluginTypes)
            .ForWebApplication()
            .UsingConfigurationCallback((builder, _) =>
            {
                builder.Configuration.AddInMemoryCollection(inMemoryConfig);
            })
            .BuildWebApplication();

        return webApplication.Services;
    }

    [Fact]
    public void WebPath_Options_WithBindConfiguration_BindsValuesFromConfiguration()
    {
        var services = BuildWebAppServices(new Dictionary<string, string?>
        {
            ["TestDatabase:ConnectionString"] = "Server=webpath;Database=WebDb",
            ["TestDatabase:CommandTimeout"] = "90",
            ["TestDatabase:EnableRetry"] = "false"
        });

        var options = services.GetRequiredService<IOptions<TestDatabaseOptions>>();

        Assert.Equal("Server=webpath;Database=WebDb", options.Value.ConnectionString);
        Assert.Equal(90, options.Value.CommandTimeout);
        Assert.False(options.Value.EnableRetry);
    }

    [Fact]
    public void WebPath_Options_WithCustomSectionName_BindsCorrectly()
    {
        var services = BuildWebAppServices(new Dictionary<string, string?>
        {
            ["Custom:Section:Value"] = "WebCustomValue",
            ["Custom:Section:Number"] = "123"
        });

        var options = services.GetRequiredService<IOptions<CustomSectionOptions>>();

        Assert.Equal("WebCustomValue", options.Value.Value);
        Assert.Equal(123, options.Value.Number);
    }

    [Fact]
    public void WebPath_NamedOptions_ResolveMultipleConfigurations()
    {
        var services = BuildWebAppServices(new Dictionary<string, string?>
        {
            ["Endpoints:Api:Url"] = "https://api.webpath.example.com",
            ["Endpoints:Api:Timeout"] = "15",
            ["Endpoints:Admin:Url"] = "https://admin.webpath.example.com",
            ["Endpoints:Admin:Timeout"] = "45"
        });

        using var scope = services.CreateScope();
        var snapshot = scope.ServiceProvider.GetRequiredService<IOptionsSnapshot<EndpointOptions>>();
        var api = snapshot.Get("Api");
        var admin = snapshot.Get("Admin");

        Assert.Equal("https://api.webpath.example.com", api.Url);
        Assert.Equal(15, api.Timeout);
        Assert.Equal("https://admin.webpath.example.com", admin.Url);
        Assert.Equal(45, admin.Timeout);
    }

    [Fact]
    public void WebPath_OptionsWithValidation_ThrowsOnInvalidValue()
    {
        var services = BuildWebAppServices(new Dictionary<string, string?>
        {
            ["ValidatedOptions:Name"] = ""
        });

        var options = services.GetRequiredService<IOptions<ValidatedTestOptions>>();

        var exception = Assert.Throws<OptionsValidationException>(() => _ = options.Value);
        Assert.Contains("Name is required", exception.Message);
    }

    [Fact]
    public void WebPath_OptionsWithValidation_PassesWhenValid()
    {
        var services = BuildWebAppServices(new Dictionary<string, string?>
        {
            ["ValidatedOptions:Name"] = "WebValidName"
        });

        var options = services.GetRequiredService<IOptions<ValidatedTestOptions>>();

        Assert.Equal("WebValidName", options.Value.Name);
    }

    [Fact]
    public void WebPath_Options_ResolveFromInjectedService()
    {
        var webApplication = new Syringe()
            .UsingGeneratedComponents(
                NexusLabs.Needlr.IntegrationTests.Generated.TypeRegistry.GetInjectableTypes,
                NexusLabs.Needlr.IntegrationTests.Generated.TypeRegistry.GetPluginTypes)
            .UsingPostPluginRegistrationCallback(services =>
                services.AddSingleton<ServiceWithOptions>())
            .ForWebApplication()
            .UsingConfigurationCallback((builder, _) =>
            {
                builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["TestDatabase:ConnectionString"] = "Server=webinjected;Database=Injected"
                });
            })
            .BuildWebApplication();

        var service = webApplication.Services.GetRequiredService<ServiceWithOptions>();

        Assert.Equal("Server=webinjected;Database=Injected", service.GetConnectionString());
    }

    [Fact]
    public void WebPath_Options_UsesDefaults_WhenSectionMissing()
    {
        var services = BuildWebAppServices(new Dictionary<string, string?>());

        var options = services.GetRequiredService<IOptions<TestDatabaseOptions>>();

        Assert.Equal("", options.Value.ConnectionString);
        Assert.Equal(30, options.Value.CommandTimeout);
        Assert.True(options.Value.EnableRetry);
    }

    [Fact]
    public void WebPath_NestedOptions_BindsCorrectly()
    {
        var services = BuildWebAppServices(new Dictionary<string, string?>
        {
            ["NestedTest:Server:Host"] = "web.db.example.com",
            ["NestedTest:Server:Port"] = "5433",
            ["NestedTest:Credentials:Username"] = "webadmin",
            ["NestedTest:Credentials:Password"] = "websecret"
        });

        var options = services.GetRequiredService<IOptions<NestedTestOptions>>();

        Assert.Equal("web.db.example.com", options.Value.Server.Host);
        Assert.Equal(5433, options.Value.Server.Port);
        Assert.Equal("webadmin", options.Value.Credentials.Username);
        Assert.Equal("websecret", options.Value.Credentials.Password);
    }

    [Fact]
    public void WebPath_ParityWithConsolePath_SameOptionsValuesForSameConfiguration()
    {
        var config = new Dictionary<string, string?>
        {
            ["TestDatabase:ConnectionString"] = "Server=parity;Database=ParityDb",
            ["TestDatabase:CommandTimeout"] = "42"
        };

        // Console path.
        var consoleProvider = new Syringe()
            .UsingGeneratedComponents(
                NexusLabs.Needlr.IntegrationTests.Generated.TypeRegistry.GetInjectableTypes,
                NexusLabs.Needlr.IntegrationTests.Generated.TypeRegistry.GetPluginTypes)
            .BuildServiceProvider(
                new ConfigurationBuilder().AddInMemoryCollection(config).Build());

        // Web path.
        var webServices = BuildWebAppServices(config);

        var consoleValue = consoleProvider
            .GetRequiredService<IOptions<TestDatabaseOptions>>().Value;
        var webValue = webServices
            .GetRequiredService<IOptions<TestDatabaseOptions>>().Value;

        Assert.Equal(consoleValue.ConnectionString, webValue.ConnectionString);
        Assert.Equal(consoleValue.CommandTimeout, webValue.CommandTimeout);
        Assert.Equal(consoleValue.EnableRetry, webValue.EnableRetry);
    }
}
