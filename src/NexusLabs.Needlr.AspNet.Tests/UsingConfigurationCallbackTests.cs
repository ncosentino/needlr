using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using NexusLabs.Needlr.Injection;

using Xunit;

namespace NexusLabs.Needlr.AspNet.Tests;

public sealed class UsingConfigurationCallbackTests
{
    [Fact]
    public void BuildWebApplication_WithConfigurationCallback_AddsInMemoryConfiguration()
    {
        var testKey = "TestSetting";
        var testValue = "TestValue123";
        var customServiceKey = "CustomService:Enabled";
        var customServiceValue = "true";
        
        var webApplication = new Syringe()
            .ForWebApplication()
            .UsingConfigurationCallback((builder, options) =>
            {
                builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    [testKey] = testValue,
                    [customServiceKey] = customServiceValue
                });
            })
            .BuildWebApplication();

        var configuration = webApplication.Services.GetRequiredService<IConfiguration>();
        Assert.NotNull(configuration);
        Assert.Equal(testValue, configuration[testKey]);
        Assert.Equal(customServiceValue, configuration[customServiceKey]);
    }

    [Fact]
    public void BuildWebApplication_WithConfigurationCallback_CanAddCustomServices()
    {
        var webApplication = new Syringe()
            .ForWebApplication()
            .UsingConfigurationCallback((builder, options) =>
            {
                builder.Services.AddSingleton<ITestService, TestService>();
                builder.Services.AddTransient<ITransientService, TransientService>();
            })
            .BuildWebApplication();

        var testService = webApplication.Services.GetService<ITestService>();
        Assert.NotNull(testService);
        Assert.IsType<TestService>(testService);

        var transientService1 = webApplication.Services.GetService<ITransientService>();
        var transientService2 = webApplication.Services.GetService<ITransientService>();
        Assert.NotNull(transientService1);
        Assert.NotNull(transientService2);
        Assert.NotSame(transientService1, transientService2);
    }

    [Fact]
    public void BuildWebApplication_WithMultipleConfigurationSources_MergesCorrectly()
    {
        var webApplication = new Syringe()
            .ForWebApplication()
            .UsingConfigurationCallback((builder, options) =>
            {
                builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Setting1"] = "Value1",
                    ["Setting2"] = "Value2",
                    ["OverriddenSetting"] = "OriginalValue"
                });
                
                builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Setting3"] = "Value3",
                    ["OverriddenSetting"] = "NewValue"
                });
            })
            .BuildWebApplication();

        var configuration = webApplication.Services.GetRequiredService<IConfiguration>();
        Assert.Equal("Value1", configuration["Setting1"]);
        Assert.Equal("Value2", configuration["Setting2"]);
        Assert.Equal("Value3", configuration["Setting3"]);
        Assert.Equal("NewValue", configuration["OverriddenSetting"]);
    }

    [Fact]
    public void BuildWebApplication_WithConfigurationCallback_CanConfigureComplexObjects()
    {
        var webApplication = new Syringe()
            .ForWebApplication()
            .UsingConfigurationCallback((builder, options) =>
            {
                builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Database:ConnectionString"] = "Server=localhost;Database=TestDb",
                    ["Database:MaxConnections"] = "100",
                    ["Features:EnableCache"] = "true",
                    ["Features:CacheTimeout"] = "300"
                });
                
                builder.Services.Configure<DatabaseOptions>(
                    builder.Configuration.GetSection("Database"));
                builder.Services.Configure<FeatureOptions>(
                    builder.Configuration.GetSection("Features"));
            })
            .BuildWebApplication();

        var configuration = webApplication.Services.GetRequiredService<IConfiguration>();
        Assert.Equal("Server=localhost;Database=TestDb", configuration["Database:ConnectionString"]);
        Assert.Equal("100", configuration["Database:MaxConnections"]);
        Assert.Equal("true", configuration["Features:EnableCache"]);
        Assert.Equal("300", configuration["Features:CacheTimeout"]);
        
        var dbOptions = webApplication.Services.GetRequiredService<IOptions<DatabaseOptions>>();
        Assert.Equal("Server=localhost;Database=TestDb", dbOptions.Value.ConnectionString);
        Assert.Equal(100, dbOptions.Value.MaxConnections);
        
        var featureOptions = webApplication.Services.GetRequiredService<IOptions<FeatureOptions>>();
        Assert.True(featureOptions.Value.EnableCache);
        Assert.Equal(300, featureOptions.Value.CacheTimeout);
    }

    [Fact]
    public void BuildWebApplication_WithConfigurationCallback_CanAccessCreateWebApplicationOptions()
    {
        string? capturedAppName = null;
        
        var webApplication = new Syringe()
            .ForWebApplication()
            .UsingOptions(() => new CreateWebApplicationOptions(
                new WebApplicationOptions { ApplicationName = "TestApplication" }))
            .UsingConfigurationCallback((builder, options) =>
            {
                capturedAppName = options.Options.ApplicationName;
                
                if (capturedAppName != null)
                {
                    builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["ApplicationName"] = capturedAppName
                    });
                }
            })
            .BuildWebApplication();

        Assert.Equal("TestApplication", capturedAppName);
        var configuration = webApplication.Services.GetRequiredService<IConfiguration>();
        Assert.Equal("TestApplication", configuration["ApplicationName"]);
    }

    [Fact]
    public void BuildWebApplication_WithoutConfigurationCallback_StillBuildsSuccessfully()
    {
        var webApplication = new Syringe()
            .ForWebApplication()
            .BuildWebApplication();

        Assert.NotNull(webApplication);
        var configuration = webApplication.Services.GetRequiredService<IConfiguration>();
        Assert.NotNull(configuration);
    }

    [Fact]
    public void BuildWebApplication_WithConfigurationCallback_CanSetBasePath()
    {
        var testBasePath = AppContext.BaseDirectory;
        
        var webApplication = new Syringe()
            .ForWebApplication()
            .UsingConfigurationCallback((builder, options) =>
            {
                builder.Configuration.SetBasePath(testBasePath);
                
                builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["BasePath"] = testBasePath
                });
            })
            .BuildWebApplication();

        var configuration = webApplication.Services.GetRequiredService<IConfiguration>();
        Assert.Equal(testBasePath, configuration["BasePath"]);
    }

    private interface ITestService
    {
        string GetValue();
    }

    private sealed class TestService : ITestService
    {
        public string GetValue() => "TestValue";
    }

    private interface ITransientService
    {
        Guid Id { get; }
    }

    private sealed class TransientService : ITransientService
    {
        public Guid Id { get; } = Guid.NewGuid();
    }

    private sealed class DatabaseOptions
    {
        public string? ConnectionString { get; set; }
        public int MaxConnections { get; set; }
    }

    private sealed class FeatureOptions
    {
        public bool EnableCache { get; set; }
        public int CacheTimeout { get; set; }
    }
}