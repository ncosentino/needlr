using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using Xunit;

namespace NexusLabs.Needlr.FluentValidation.Tests;

public sealed class FluentValidationServiceCollectionExtensionsTests
{
    [Fact]
    public void AddFluentValidationOptionsAdapter_RegistersValidator()
    {
        var services = new ServiceCollection();
        services.AddFluentValidationOptionsAdapter<TestOptions, TestOptionsValidator>();

        var provider = services.BuildServiceProvider();
        var validateOptions = provider.GetService<IValidateOptions<TestOptions>>();

        Assert.NotNull(validateOptions);
    }

    [Fact]
    public void AddFluentValidationOptionsAdapter_IntegratesWithOptions()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Test:Name"] = ""
            })
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddOptions<TestOptions>()
            .BindConfiguration("Test")
            .ValidateOnStart();
        services.AddFluentValidationOptionsAdapter<TestOptions, TestOptionsValidator>();

        var provider = services.BuildServiceProvider();

        var options = provider.GetRequiredService<IOptions<TestOptions>>();
        var exception = Assert.Throws<OptionsValidationException>(() => _ = options.Value);
        Assert.Contains("Name", exception.Message);
    }

    [Fact]
    public void AddFluentValidationOptionsAdapter_WithValidConfig_Succeeds()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Test:Name"] = "ValidName"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddOptions<TestOptions>()
            .BindConfiguration("Test");
        services.AddFluentValidationOptionsAdapter<TestOptions, TestOptionsValidator>();

        var provider = services.BuildServiceProvider();

        var options = provider.GetRequiredService<IOptions<TestOptions>>();
        Assert.Equal("ValidName", options.Value.Name);
    }
}
