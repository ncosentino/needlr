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
/// These tests verify the generated code actually works with real DI containers
/// using the proper Syringe fluent API.
/// </summary>
public sealed class OptionsSourceGenTests
{
    private static IServiceProvider BuildProvider(IConfiguration configuration)
    {
        return new Syringe()
            .UsingGeneratedComponents(
                NexusLabs.Needlr.IntegrationTests.Generated.TypeRegistry.GetInjectableTypes,
                NexusLabs.Needlr.IntegrationTests.Generated.TypeRegistry.GetPluginTypes)
            .BuildServiceProvider(configuration);
    }

    [Fact]
    public void Options_CanResolveIOptions_WithConfigurationBinding()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["TestDatabase:ConnectionString"] = "Server=localhost;Database=TestDb",
                ["TestDatabase:CommandTimeout"] = "60",
                ["TestDatabase:EnableRetry"] = "false"
            })
            .Build();

        var provider = BuildProvider(configuration);

        var options = provider.GetRequiredService<IOptions<TestDatabaseOptions>>();

        Assert.NotNull(options);
        Assert.Equal("Server=localhost;Database=TestDb", options.Value.ConnectionString);
        Assert.Equal(60, options.Value.CommandTimeout);
        Assert.False(options.Value.EnableRetry);
    }

    [Fact]
    public void Options_CanResolveIOptionsSnapshot_ForScopedAccess()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["TestDatabase:ConnectionString"] = "Server=prod;Database=ProdDb"
            })
            .Build();

        var provider = BuildProvider(configuration);

        using var scope = provider.CreateScope();
        var snapshot = scope.ServiceProvider.GetRequiredService<IOptionsSnapshot<TestDatabaseOptions>>();

        Assert.NotNull(snapshot);
        Assert.Equal("Server=prod;Database=ProdDb", snapshot.Value.ConnectionString);
    }

    [Fact]
    public void Options_CanResolveIOptionsMonitor_ForChangeTracking()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["TestDatabase:ConnectionString"] = "Server=monitor;Database=MonitorDb"
            })
            .Build();

        var provider = BuildProvider(configuration);

        var monitor = provider.GetRequiredService<IOptionsMonitor<TestDatabaseOptions>>();

        Assert.NotNull(monitor);
        Assert.Equal("Server=monitor;Database=MonitorDb", monitor.CurrentValue.ConnectionString);
    }

    [Fact]
    public void Options_UsesDefaultValues_WhenNotConfigured()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();

        var provider = BuildProvider(configuration);

        var options = provider.GetRequiredService<IOptions<TestDatabaseOptions>>();

        Assert.Equal("", options.Value.ConnectionString);
        Assert.Equal(30, options.Value.CommandTimeout);
        Assert.True(options.Value.EnableRetry);
    }

    [Fact]
    public void Options_WithCustomSectionName_BindsCorrectly()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Custom:Section:Value"] = "CustomValue",
                ["Custom:Section:Number"] = "42"
            })
            .Build();

        var provider = BuildProvider(configuration);

        var options = provider.GetRequiredService<IOptions<CustomSectionOptions>>();

        Assert.Equal("CustomValue", options.Value.Value);
        Assert.Equal(42, options.Value.Number);
    }

    [Fact]
    public void Options_NamedOptions_CanResolveMultipleConfigurations()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Endpoints:Api:Url"] = "https://api.example.com",
                ["Endpoints:Api:Timeout"] = "30",
                ["Endpoints:Admin:Url"] = "https://admin.example.com",
                ["Endpoints:Admin:Timeout"] = "60"
            })
            .Build();

        var provider = BuildProvider(configuration);

        using var scope = provider.CreateScope();
        var snapshot = scope.ServiceProvider.GetRequiredService<IOptionsSnapshot<EndpointOptions>>();
        var apiOptions = snapshot.Get("Api");
        var adminOptions = snapshot.Get("Admin");

        Assert.Equal("https://api.example.com", apiOptions.Url);
        Assert.Equal(30, apiOptions.Timeout);
        Assert.Equal("https://admin.example.com", adminOptions.Url);
        Assert.Equal(60, adminOptions.Timeout);
    }

    [Fact]
    public void Options_WithValidation_ValidatesOnAccess()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ValidatedOptions:Name"] = ""
            })
            .Build();

        var provider = BuildProvider(configuration);

        var options = provider.GetRequiredService<IOptions<ValidatedTestOptions>>();
        var exception = Assert.Throws<OptionsValidationException>(() => _ = options.Value);
        Assert.Contains("Name is required", exception.Message);
    }

    [Fact]
    public void Options_WithValidation_PassesWhenValid()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ValidatedOptions:Name"] = "ValidName"
            })
            .Build();

        var provider = BuildProvider(configuration);

        var options = provider.GetRequiredService<IOptions<ValidatedTestOptions>>();

        Assert.Equal("ValidName", options.Value.Name);
    }

    [Fact]
    public void Options_WithExternalValidator_UsesValidatorType()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ExternallyValidated:Email"] = "not-an-email"
            })
            .Build();

        var provider = BuildProvider(configuration);

        var options = provider.GetRequiredService<IOptions<ExternallyValidatedOptions>>();
        var exception = Assert.Throws<OptionsValidationException>(() => _ = options.Value);
        Assert.Contains("Email", exception.Message);
    }

    [Fact]
    public void Options_NestedConfiguration_BindsCorrectly()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["NestedTest:Server:Host"] = "db.example.com",
                ["NestedTest:Server:Port"] = "5432",
                ["NestedTest:Credentials:Username"] = "admin",
                ["NestedTest:Credentials:Password"] = "secret"
            })
            .Build();

        var provider = BuildProvider(configuration);

        var options = provider.GetRequiredService<IOptions<NestedTestOptions>>();

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
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ArrayTest:Tags:0"] = "tag1",
                ["ArrayTest:Tags:1"] = "tag2",
                ["ArrayTest:Tags:2"] = "tag3"
            })
            .Build();

        var provider = BuildProvider(configuration);

        var options = provider.GetRequiredService<IOptions<ArrayTestOptions>>();

        Assert.NotNull(options.Value.Tags);
        Assert.Equal(3, options.Value.Tags.Length);
        Assert.Equal("tag1", options.Value.Tags[0]);
        Assert.Equal("tag2", options.Value.Tags[1]);
        Assert.Equal("tag3", options.Value.Tags[2]);
    }

    [Fact]
    public void Options_DictionaryConfiguration_BindsCorrectly()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DictTest:Settings:Key1"] = "Value1",
                ["DictTest:Settings:Key2"] = "Value2"
            })
            .Build();

        var provider = BuildProvider(configuration);

        var options = provider.GetRequiredService<IOptions<DictionaryTestOptions>>();

        Assert.NotNull(options.Value.Settings);
        Assert.Equal(2, options.Value.Settings.Count);
        Assert.Equal("Value1", options.Value.Settings["Key1"]);
        Assert.Equal("Value2", options.Value.Settings["Key2"]);
    }

    [Fact]
    public void Options_CanInjectIntoService()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["TestDatabase:ConnectionString"] = "Server=injected;Database=InjectedDb"
            })
            .Build();

        var provider = new Syringe()
            .UsingGeneratedComponents(
                NexusLabs.Needlr.IntegrationTests.Generated.TypeRegistry.GetInjectableTypes,
                NexusLabs.Needlr.IntegrationTests.Generated.TypeRegistry.GetPluginTypes)
            .UsingPostPluginRegistrationCallback(services =>
                services.AddSingleton<ServiceWithOptions>())
            .BuildServiceProvider(configuration);

        var service = provider.GetRequiredService<ServiceWithOptions>();

        Assert.Equal("Server=injected;Database=InjectedDb", service.GetConnectionString());
    }

    [Fact]
    public void Options_VerifiesSyringeIntegration_EndToEnd()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["TestDatabase:ConnectionString"] = "Server=e2e;Database=EndToEnd",
                ["TestDatabase:CommandTimeout"] = "120"
            })
            .Build();

        var provider = new Syringe()
            .UsingGeneratedComponents(
                NexusLabs.Needlr.IntegrationTests.Generated.TypeRegistry.GetInjectableTypes,
                NexusLabs.Needlr.IntegrationTests.Generated.TypeRegistry.GetPluginTypes)
            .BuildServiceProvider(configuration);

        var options = provider.GetRequiredService<IOptions<TestDatabaseOptions>>();
        var monitor = provider.GetRequiredService<IOptionsMonitor<TestDatabaseOptions>>();
        
        using var scope = provider.CreateScope();
        var snapshot = scope.ServiceProvider.GetRequiredService<IOptionsSnapshot<TestDatabaseOptions>>();

        Assert.Equal("Server=e2e;Database=EndToEnd", options.Value.ConnectionString);
        Assert.Equal("Server=e2e;Database=EndToEnd", monitor.CurrentValue.ConnectionString);
        Assert.Equal("Server=e2e;Database=EndToEnd", snapshot.Value.ConnectionString);
        Assert.Equal(120, options.Value.CommandTimeout);
    }

    [Fact]
    public void Options_ImmutableClass_BindsInitOnlyProperties()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ImmutableClass:Host"] = "api.example.com",
                ["ImmutableClass:Port"] = "9000",
                ["ImmutableClass:ApiKey"] = "secret-key-123"
            })
            .Build();

        var provider = BuildProvider(configuration);

        var options = provider.GetRequiredService<IOptions<ImmutableClassOptions>>();

        Assert.Equal("api.example.com", options.Value.Host);
        Assert.Equal(9000, options.Value.Port);
        Assert.Equal("secret-key-123", options.Value.ApiKey);
    }

    [Fact]
    public void Options_ImmutableClass_UsesDefaults_WhenNotConfigured()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ImmutableClass:ApiKey"] = "required-key"
            })
            .Build();

        var provider = BuildProvider(configuration);

        var options = provider.GetRequiredService<IOptions<ImmutableClassOptions>>();

        Assert.Equal("", options.Value.Host);
        Assert.Equal(8080, options.Value.Port);
        Assert.Equal("required-key", options.Value.ApiKey);
    }

    [Fact]
    public void Options_ImmutableRecord_BindsCorrectly()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ImmutableRecord:Endpoint"] = "https://api.example.com",
                ["ImmutableRecord:Timeout"] = "120"
            })
            .Build();

        var provider = BuildProvider(configuration);

        var options = provider.GetRequiredService<IOptions<ImmutableRecordOptions>>();

        Assert.Equal("https://api.example.com", options.Value.Endpoint);
        Assert.Equal(120, options.Value.Timeout);
    }

    [Fact]
    public void Options_ImmutableRecord_WithIOptionsMonitor_ReturnsCurrentValue()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ImmutableRecord:Endpoint"] = "https://monitor.example.com",
                ["ImmutableRecord:Timeout"] = "60"
            })
            .Build();

        var provider = BuildProvider(configuration);

        var monitor = provider.GetRequiredService<IOptionsMonitor<ImmutableRecordOptions>>();

        Assert.Equal("https://monitor.example.com", monitor.CurrentValue.Endpoint);
        Assert.Equal(60, monitor.CurrentValue.Timeout);
    }

    [Fact]
    public void Options_ImmutableTypes_CanInjectIntoService()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ImmutableRecord:Endpoint"] = "https://injected.example.com",
                ["ImmutableRecord:Timeout"] = "45"
            })
            .Build();

        var provider = new Syringe()
            .UsingGeneratedComponents(
                NexusLabs.Needlr.IntegrationTests.Generated.TypeRegistry.GetInjectableTypes,
                NexusLabs.Needlr.IntegrationTests.Generated.TypeRegistry.GetPluginTypes)
            .UsingPostPluginRegistrationCallback(services =>
                services.AddSingleton<ServiceWithImmutableOptions>())
            .BuildServiceProvider(configuration);

        var service = provider.GetRequiredService<ServiceWithImmutableOptions>();

        Assert.Equal("https://injected.example.com", service.GetEndpoint());
        Assert.Equal(45, service.GetTimeout());
    }
}

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

public class ServiceWithOptions
{
    private readonly IOptions<TestDatabaseOptions> _options;

    public ServiceWithOptions(IOptions<TestDatabaseOptions> options)
    {
        _options = options;
    }

    public string GetConnectionString() => _options.Value.ConnectionString;
}

/// <summary>
/// Tests immutable options class with init-only properties.
/// </summary>
[Options("ImmutableClass")]
public class ImmutableClassOptions
{
    public string Host { get; init; } = "";
    public int Port { get; init; } = 8080;
    public required string ApiKey { get; init; }
}

/// <summary>
/// Tests record type with init properties.
/// Note: Positional records (e.g., record Foo(string Bar)) do NOT work with reflection-based
/// configuration binding because they lack parameterless constructors. Use init-only records instead.
/// </summary>
[Options("ImmutableRecord")]
public record ImmutableRecordOptions
{
    public string Endpoint { get; init; } = "";
    public int Timeout { get; init; } = 30;
}

public class ServiceWithImmutableOptions
{
    private readonly IOptions<ImmutableRecordOptions> _options;

    public ServiceWithImmutableOptions(IOptions<ImmutableRecordOptions> options)
    {
        _options = options;
    }

    public string GetEndpoint() => _options.Value.Endpoint;
    public int GetTimeout() => _options.Value.Timeout;
}
