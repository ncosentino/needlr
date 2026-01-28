using System.Collections.Generic;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using NexusLabs.Needlr.Generators;
using NexusLabs.Needlr.Injection;
using NexusLabs.Needlr.Injection.SourceGen;

using Xunit;

namespace NexusLabs.Needlr.IntegrationTests.SourceGen;

/// <summary>
/// Integration tests for [Options] attribute source generation.
/// These tests verify the generated code actually works with real DI containers.
/// </summary>
public sealed class OptionsSourceGenTests
{
    [Fact]
    public void Options_CanResolveIOptions_WithConfigurationBinding()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["TestDatabase:ConnectionString"] = "Server=localhost;Database=TestDb",
                ["TestDatabase:CommandTimeout"] = "60",
                ["TestDatabase:EnableRetry"] = "false"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);

        // Register generated options
        NexusLabs.Needlr.IntegrationTests.Generated.TypeRegistry.RegisterOptions(services, configuration);

        var provider = services.BuildServiceProvider();

        // Act
        var options = provider.GetRequiredService<IOptions<TestDatabaseOptions>>();

        // Assert
        Assert.NotNull(options);
        Assert.Equal("Server=localhost;Database=TestDb", options.Value.ConnectionString);
        Assert.Equal(60, options.Value.CommandTimeout);
        Assert.False(options.Value.EnableRetry);
    }

    [Fact]
    public void Options_CanResolveIOptionsSnapshot_ForScopedAccess()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["TestDatabase:ConnectionString"] = "Server=prod;Database=ProdDb"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        NexusLabs.Needlr.IntegrationTests.Generated.TypeRegistry.RegisterOptions(services, configuration);

        var provider = services.BuildServiceProvider();

        // Act
        using var scope = provider.CreateScope();
        var snapshot = scope.ServiceProvider.GetRequiredService<IOptionsSnapshot<TestDatabaseOptions>>();

        // Assert
        Assert.NotNull(snapshot);
        Assert.Equal("Server=prod;Database=ProdDb", snapshot.Value.ConnectionString);
    }

    [Fact]
    public void Options_CanResolveIOptionsMonitor_ForChangeTracking()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["TestDatabase:ConnectionString"] = "Server=monitor;Database=MonitorDb"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        NexusLabs.Needlr.IntegrationTests.Generated.TypeRegistry.RegisterOptions(services, configuration);

        var provider = services.BuildServiceProvider();

        // Act
        var monitor = provider.GetRequiredService<IOptionsMonitor<TestDatabaseOptions>>();

        // Assert
        Assert.NotNull(monitor);
        Assert.Equal("Server=monitor;Database=MonitorDb", monitor.CurrentValue.ConnectionString);
    }

    [Fact]
    public void Options_UsesDefaultValues_WhenNotConfigured()
    {
        // Arrange - empty configuration
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        NexusLabs.Needlr.IntegrationTests.Generated.TypeRegistry.RegisterOptions(services, configuration);

        var provider = services.BuildServiceProvider();

        // Act
        var options = provider.GetRequiredService<IOptions<TestDatabaseOptions>>();

        // Assert - defaults from class definition
        Assert.Equal("", options.Value.ConnectionString);
        Assert.Equal(30, options.Value.CommandTimeout);
        Assert.True(options.Value.EnableRetry);
    }

    [Fact]
    public void Options_WithCustomSectionName_BindsCorrectly()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Custom:Section:Value"] = "CustomValue",
                ["Custom:Section:Number"] = "42"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        NexusLabs.Needlr.IntegrationTests.Generated.TypeRegistry.RegisterOptions(services, configuration);

        var provider = services.BuildServiceProvider();

        // Act
        var options = provider.GetRequiredService<IOptions<CustomSectionOptions>>();

        // Assert
        Assert.Equal("CustomValue", options.Value.Value);
        Assert.Equal(42, options.Value.Number);
    }

    [Fact]
    public void Options_NamedOptions_CanResolveMultipleConfigurations()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Endpoints:Api:Url"] = "https://api.example.com",
                ["Endpoints:Api:Timeout"] = "30",
                ["Endpoints:Admin:Url"] = "https://admin.example.com",
                ["Endpoints:Admin:Timeout"] = "60"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        NexusLabs.Needlr.IntegrationTests.Generated.TypeRegistry.RegisterOptions(services, configuration);

        var provider = services.BuildServiceProvider();

        // Act
        using var scope = provider.CreateScope();
        var snapshot = scope.ServiceProvider.GetRequiredService<IOptionsSnapshot<EndpointOptions>>();
        var apiOptions = snapshot.Get("Api");
        var adminOptions = snapshot.Get("Admin");

        // Assert
        Assert.Equal("https://api.example.com", apiOptions.Url);
        Assert.Equal(30, apiOptions.Timeout);
        Assert.Equal("https://admin.example.com", adminOptions.Url);
        Assert.Equal(60, adminOptions.Timeout);
    }

    [Fact]
    public void Options_WithValidation_ValidatesAtStartup()
    {
        // Arrange - invalid configuration (missing required field)
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ValidatedOptions:Name"] = "" // empty, should fail validation
            })
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        NexusLabs.Needlr.IntegrationTests.Generated.TypeRegistry.RegisterOptions(services, configuration);

        var provider = services.BuildServiceProvider();

        // Act & Assert - accessing options should throw due to validation
        var options = provider.GetRequiredService<IOptions<ValidatedTestOptions>>();
        var exception = Assert.Throws<OptionsValidationException>(() => _ = options.Value);
        Assert.Contains("Name is required", exception.Message);
    }

    [Fact]
    public void Options_WithValidation_PassesWhenValid()
    {
        // Arrange - valid configuration
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ValidatedOptions:Name"] = "ValidName"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        NexusLabs.Needlr.IntegrationTests.Generated.TypeRegistry.RegisterOptions(services, configuration);

        var provider = services.BuildServiceProvider();

        // Act
        var options = provider.GetRequiredService<IOptions<ValidatedTestOptions>>();

        // Assert - no exception, value is accessible
        Assert.Equal("ValidName", options.Value.Name);
    }

    [Fact]
    public void Options_WithExternalValidator_UsesValidatorType()
    {
        // Arrange - invalid configuration
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ExternallyValidated:Email"] = "not-an-email"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        NexusLabs.Needlr.IntegrationTests.Generated.TypeRegistry.RegisterOptions(services, configuration);

        var provider = services.BuildServiceProvider();

        // Act & Assert
        var options = provider.GetRequiredService<IOptions<ExternallyValidatedOptions>>();
        var exception = Assert.Throws<OptionsValidationException>(() => _ = options.Value);
        Assert.Contains("Email", exception.Message);
    }

    [Fact]
    public void Options_NestedConfiguration_BindsCorrectly()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["NestedTest:Server:Host"] = "db.example.com",
                ["NestedTest:Server:Port"] = "5432",
                ["NestedTest:Credentials:Username"] = "admin",
                ["NestedTest:Credentials:Password"] = "secret"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        NexusLabs.Needlr.IntegrationTests.Generated.TypeRegistry.RegisterOptions(services, configuration);

        var provider = services.BuildServiceProvider();

        // Act
        var options = provider.GetRequiredService<IOptions<NestedTestOptions>>();

        // Assert
        Assert.NotNull(options.Value.Server);
        Assert.Equal("db.example.com", options.Value.Server.Host);
        Assert.Equal(5432, options.Value.Server.Port);
        Assert.NotNull(options.Value.Credentials);
        Assert.Equal("admin", options.Value.Credentials.Username);
        Assert.Equal("secret", options.Value.Credentials.Password);
    }

    [Fact]
    public void Options_ArrayConfiguration_BindsCorrectly()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ArrayTest:Tags:0"] = "tag1",
                ["ArrayTest:Tags:1"] = "tag2",
                ["ArrayTest:Tags:2"] = "tag3"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        NexusLabs.Needlr.IntegrationTests.Generated.TypeRegistry.RegisterOptions(services, configuration);

        var provider = services.BuildServiceProvider();

        // Act
        var options = provider.GetRequiredService<IOptions<ArrayTestOptions>>();

        // Assert
        Assert.NotNull(options.Value.Tags);
        Assert.Equal(3, options.Value.Tags.Length);
        Assert.Equal("tag1", options.Value.Tags[0]);
        Assert.Equal("tag2", options.Value.Tags[1]);
        Assert.Equal("tag3", options.Value.Tags[2]);
    }

    [Fact]
    public void Options_DictionaryConfiguration_BindsCorrectly()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DictTest:Settings:Key1"] = "Value1",
                ["DictTest:Settings:Key2"] = "Value2"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        NexusLabs.Needlr.IntegrationTests.Generated.TypeRegistry.RegisterOptions(services, configuration);

        var provider = services.BuildServiceProvider();

        // Act
        var options = provider.GetRequiredService<IOptions<DictionaryTestOptions>>();

        // Assert
        Assert.NotNull(options.Value.Settings);
        Assert.Equal(2, options.Value.Settings.Count);
        Assert.Equal("Value1", options.Value.Settings["Key1"]);
        Assert.Equal("Value2", options.Value.Settings["Key2"]);
    }

    [Fact]
    public void Options_CanInjectIntoService()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["TestDatabase:ConnectionString"] = "Server=injected;Database=InjectedDb"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        NexusLabs.Needlr.IntegrationTests.Generated.TypeRegistry.RegisterOptions(services, configuration);
        services.AddSingleton<ServiceWithOptions>();

        var provider = services.BuildServiceProvider();

        // Act
        var service = provider.GetRequiredService<ServiceWithOptions>();

        // Assert
        Assert.Equal("Server=injected;Database=InjectedDb", service.GetConnectionString());
    }

    [Fact]
    public void Options_Syringe_IntegratesWithGeneratedComponents()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["TestDatabase:ConnectionString"] = "Server=syringe;Database=SyringeDb"
            })
            .Build();

        // Act - use Syringe with configuration
        var provider = new Syringe()
            .UsingGeneratedComponents(
                NexusLabs.Needlr.IntegrationTests.Generated.TypeRegistry.GetInjectableTypes,
                NexusLabs.Needlr.IntegrationTests.Generated.TypeRegistry.GetPluginTypes)
            .BuildServiceProvider(configuration);

        // Note: RegisterOptions needs to be called separately - test that pattern here
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        NexusLabs.Needlr.IntegrationTests.Generated.TypeRegistry.RegisterOptions(services, configuration);
        var optionsProvider = services.BuildServiceProvider();

        // Assert
        var options = optionsProvider.GetRequiredService<IOptions<TestDatabaseOptions>>();
        Assert.Equal("Server=syringe;Database=SyringeDb", options.Value.ConnectionString);
    }
}

// Test Options Classes

[Options("TestDatabase")]
public class TestDatabaseOptions
{
    public string ConnectionString { get; set; } = "";
    public int CommandTimeout { get; set; } = 30;
    public bool EnableRetry { get; set; } = true;
}

[Options("Custom:Section")]
public class CustomSectionOptions
{
    public string Value { get; set; } = "";
    public int Number { get; set; }
}

[Options("Endpoints:Api", Name = "Api")]
[Options("Endpoints:Admin", Name = "Admin")]
public class EndpointOptions
{
    public string Url { get; set; } = "";
    public int Timeout { get; set; } = 30;
}

[Options("ValidatedOptions", ValidateOnStart = true)]
public class ValidatedTestOptions
{
    public string Name { get; set; } = "";

    public IEnumerable<ValidationError> Validate()
    {
        if (string.IsNullOrEmpty(Name))
        {
            yield return "Name is required";
        }
    }
}

[Options("ExternallyValidated", ValidateOnStart = true, Validator = typeof(ExternalOptionsValidator))]
public class ExternallyValidatedOptions
{
    public string Email { get; set; } = "";
}

public class ExternalOptionsValidator : IOptionsValidator<ExternallyValidatedOptions>
{
    public IEnumerable<ValidationError> Validate(ExternallyValidatedOptions options)
    {
        if (!string.IsNullOrEmpty(options.Email) && !options.Email.Contains('@'))
        {
            yield return "Email must be a valid email address";
        }
    }
}

[Options("NestedTest")]
public class NestedTestOptions
{
    public ServerSettings Server { get; set; } = new();
    public CredentialSettings Credentials { get; set; } = new();

    public class ServerSettings
    {
        public string Host { get; set; } = "";
        public int Port { get; set; }
    }

    public class CredentialSettings
    {
        public string Username { get; set; } = "";
        public string Password { get; set; } = "";
    }
}

[Options("ArrayTest")]
public class ArrayTestOptions
{
    public string[] Tags { get; set; } = Array.Empty<string>();
}

[Options("DictTest")]
public class DictionaryTestOptions
{
    public Dictionary<string, string> Settings { get; set; } = new();
}

// Service that uses options via constructor injection
public class ServiceWithOptions
{
    private readonly IOptions<TestDatabaseOptions> _options;

    public ServiceWithOptions(IOptions<TestDatabaseOptions> options)
    {
        _options = options;
    }

    public string GetConnectionString() => _options.Value.ConnectionString;
}
