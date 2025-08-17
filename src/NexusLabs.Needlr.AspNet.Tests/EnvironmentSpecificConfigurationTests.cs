using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using NexusLabs.Needlr.Injection;

using Xunit;

namespace NexusLabs.Needlr.AspNet.Tests;

/// <summary>
/// Tests for environment-specific configuration using UsingConfigurationCallback,
/// demonstrating the pattern used in the AspNetCoreApp1 example.
/// </summary>
public sealed class EnvironmentSpecificConfigurationTests
{
    [Fact]
    public void BuildWebApplication_WithEnvironmentSpecificConfiguration_LoadsCorrectSettings()
    {
        var environmentName = "Development";
        var baseConfigValue = "BaseValue";
        var envConfigValue = "DevelopmentValue";
        var webApplication = new Syringe()
            .ForWebApplication()
            .UsingOptions(() => new CreateWebApplicationOptions(
                new WebApplicationOptions { EnvironmentName = environmentName }))
            .UsingConfigurationCallback((builder, options) =>
            {
                builder.Configuration
                    .SetBasePath(AppContext.BaseDirectory)
                    .AddEnvironmentVariables();

                builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Setting1"] = baseConfigValue,
                    ["Setting2"] = "BaseOnly",
                    ["Environment"] = "Base"
                });

                if (builder.Environment.EnvironmentName == "Development")
                {
                    builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["Setting1"] = envConfigValue,
                        ["DevelopmentOnly"] = "DevValue",
                        ["Environment"] = "Development"
                    });
                }
            })
            .BuildWebApplication();

        var configuration = webApplication.Services.GetRequiredService<IConfiguration>();
        Assert.Equal(envConfigValue, configuration["Setting1"]);

        Assert.Equal("BaseOnly", configuration["Setting2"]);
        Assert.Equal("DevValue", configuration["DevelopmentOnly"]);
        Assert.Equal("Development", configuration["Environment"]);
    }

    [Theory]
    [InlineData("Test", false)]
    [InlineData("Development", true)]
    [InlineData("Production", true)]
    public void BuildWebApplication_WithConditionalConfiguration_LoadsBasedOnEnvironment(
        string environmentName, 
        bool shouldLoadBaseConfig)
    {
        var webApplication = new Syringe()
            .ForWebApplication()
            .UsingOptions(() => new CreateWebApplicationOptions(
                new WebApplicationOptions { EnvironmentName = environmentName }))
            .UsingConfigurationCallback((builder, options) =>
            {
                if (!builder.Environment.IsEnvironment("Test"))
                {
                    builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["BaseConfigLoaded"] = "true",
                        ["ConnectionString"] = "Server=localhost;Database=MyApp"
                    });
                }
                builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    [$"{environmentName}:Loaded"] = "true"
                });
            })
            .BuildWebApplication();

        var configuration = webApplication.Services.GetRequiredService<IConfiguration>();
        
        if (shouldLoadBaseConfig)
        {
            Assert.Equal("true", configuration["BaseConfigLoaded"]);
            Assert.Equal("Server=localhost;Database=MyApp", configuration["ConnectionString"]);
        }
        else
        {
            Assert.Null(configuration["BaseConfigLoaded"]);
            Assert.Null(configuration["ConnectionString"]);
        }
        
        Assert.Equal("true", configuration[$"{environmentName}:Loaded"]);
    }

    [Fact]
    public void BuildWebApplication_WithWeatherConfiguration_LoadsWeatherSettings()
    {
        var temperatureCelsius = 25.5;
        var summary = "Sunny";
        var webApplication = new Syringe()
            .ForWebApplication()
            .UsingConfigurationCallback((builder, options) =>
            {
                builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Weather:TemperatureCelsius"] = temperatureCelsius.ToString(),
                    ["Weather:Summary"] = summary
                });
            })
            .BuildWebApplication();

        var configuration = webApplication.Services.GetRequiredService<IConfiguration>();
        var weatherSection = configuration.GetSection("Weather");
        
        Assert.Equal(temperatureCelsius.ToString(), weatherSection["TemperatureCelsius"]);
        Assert.Equal(summary, weatherSection["Summary"]);
        var weatherConfig = new WeatherConfiguration();
        weatherSection.Bind(weatherConfig);
        
        Assert.Equal(temperatureCelsius, weatherConfig.TemperatureCelsius);
        Assert.Equal(summary, weatherConfig.Summary);
    }

    [Fact]
    public void BuildWebApplication_WithOverridingConfiguration_LastSourceWins()
    {
        var webApplication = new Syringe()
            .ForWebApplication()
            .UsingConfigurationCallback((builder, options) =>
            {
                builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ApiUrl"] = "https://api.example.com",
                    ["Timeout"] = "30"
                });
                
                builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ApiUrl"] = "https://api.dev.example.com",
                    ["DebugMode"] = "true"
                });
                
                builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Timeout"] = "60"
                });
            })
            .BuildWebApplication();

        var configuration = webApplication.Services.GetRequiredService<IConfiguration>();
        Assert.Equal("https://api.dev.example.com", configuration["ApiUrl"]);
        Assert.Equal("60", configuration["Timeout"]);
        Assert.Equal("true", configuration["DebugMode"]);
    }

    private sealed class WeatherConfiguration
    {
        public double TemperatureCelsius { get; set; }
        public string? Summary { get; set; }
    }
}