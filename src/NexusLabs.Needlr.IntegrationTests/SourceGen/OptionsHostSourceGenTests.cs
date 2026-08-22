using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

using NexusLabs.Needlr.Generators;
using NexusLabs.Needlr.Hosting;
using NexusLabs.Needlr.Injection;
using NexusLabs.Needlr.Injection.SourceGen;

using Xunit;

namespace NexusLabs.Needlr.IntegrationTests.SourceGen;

public sealed class OptionsHostSourceGenTests
{
    [Fact]
    public async Task Options_HostAndServiceProvider_BindSameConfiguredValues()
    {
        var values = new Dictionary<string, string?>
        {
            ["GeneratedHostWorker:Enabled"] = "true",
            ["ValidatedOptions:Name"] = "ValidName",
            ["ExternallyValidated:Email"] = "valid@example.com"
        };
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();

        var provider = CreateSyringe().BuildServiceProvider(configuration);
        using var host = CreateSyringe()
            .ForHost()
            .UsingConfigurationCallback((builder, _) =>
            {
                builder.Configuration.AddInMemoryCollection(values);
                builder.Services.AddSingleton<GeneratedOptionsHostedService>();
                builder.Services.AddSingleton<IHostedService>(
                    services => services.GetRequiredService<GeneratedOptionsHostedService>());
            })
            .BuildHost();

        var providerOptions = provider
            .GetRequiredService<IOptions<GeneratedHostWorkerOptions>>()
            .Value;
        var hostedService = host.Services
            .GetRequiredService<GeneratedOptionsHostedService>();

        Assert.True(providerOptions.Enabled, "The service-provider path should bind the configured value.");
        Assert.Null(hostedService.EnabledAtStart);

        await host.StartAsync(TestContext.Current.CancellationToken);

        Assert.True(
            hostedService.EnabledAtStart is true,
            "The host path should bind options before hosted services start.");

        await host.StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Options_HostValidationFailure_StopsStartup()
    {
        var values = new Dictionary<string, string?>
        {
            ["ValidatedOptions:Name"] = "ValidName",
            ["ExternallyValidated:Email"] = "valid@example.com"
        };
        using var host = CreateSyringe()
            .ForHost()
            .UsingConfigurationCallback((builder, _) =>
                builder.Configuration.AddInMemoryCollection(values))
            .BuildHost();

        var exception = await Assert.ThrowsAsync<OptionsValidationException>(
            () => host.StartAsync(TestContext.Current.CancellationToken));

        Assert.Contains("Generated host worker must be enabled.", exception.Message);
    }

    private static ConfiguredSyringe CreateSyringe()
    {
        return new Syringe()
            .UsingGeneratedComponents(
                NexusLabs.Needlr.IntegrationTests.Generated.TypeRegistry.GetInjectableTypes,
                NexusLabs.Needlr.IntegrationTests.Generated.TypeRegistry.GetPluginTypes);
    }
}

/// <summary>
/// Provides validated options for generic-host source-generation tests.
/// </summary>
[Options("GeneratedHostWorker", ValidateOnStart = true)]
public sealed record GeneratedHostWorkerOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether the generated host worker is enabled.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Validates that the generated host worker is enabled.
    /// </summary>
    /// <returns>The validation errors.</returns>
    public IEnumerable<ValidationError> Validate()
    {
        if (!Enabled)
        {
            yield return "Generated host worker must be enabled.";
        }
    }
}

/// <summary>
/// Captures the generated options value observed when the host starts.
/// </summary>
[DoNotAutoRegister]
internal sealed class GeneratedOptionsHostedService(
    IOptions<GeneratedHostWorkerOptions> options) :
    IHostedService
{
    public bool? EnabledAtStart { get; private set; }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        EnabledAtStart = options.Value.Enabled;
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
