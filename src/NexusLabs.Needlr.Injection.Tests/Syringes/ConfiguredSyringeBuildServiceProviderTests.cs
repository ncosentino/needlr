using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using NexusLabs.Needlr.Generators;
using NexusLabs.Needlr.Injection.SourceGen;
using NexusLabs.Needlr.Injection.Tests.Bootstrap;
using NexusLabs.Needlr.Injection.TypeFilterers;

using Xunit;

namespace NexusLabs.Needlr.Injection.Tests.Syringes;

/// <summary>
/// End-to-end coverage for <see cref="ConfiguredSyringe.BuildServiceProvider(IConfiguration)"/>:
/// real service resolution, callback ordering relative to the source-generated options and
/// extension registrars, and verification behavior.
/// </summary>
[Collection(SourceGenBootstrapCollection.Name)]
public sealed class ConfiguredSyringeBuildServiceProviderTests : IDisposable
{
    private readonly IConfiguration _configuration = new ConfigurationBuilder().Build();

    public ConfiguredSyringeBuildServiceProviderTests()
    {
        NeedlrSourceGenBootstrap.ClearRegistrationsForTesting();
        SourceGenRegistry.Clear();
    }

    public void Dispose()
    {
        NeedlrSourceGenBootstrap.ClearRegistrationsForTesting();
        SourceGenRegistry.Clear();
    }

    [Fact]
    public void BuildServiceProvider_WithGeneratedComponents_ResolvesRealSingletonService()
    {
        using var scope = NeedlrSourceGenBootstrap.BeginTestScope(
            () =>
            [
                new InjectableTypeInfo(
                    typeof(SyringeTestService),
                    [typeof(ISyringeTestService)],
                    InjectableLifetime.Singleton,
                    _ => new SyringeTestService())
            ],
            () => []);

        var provider = new Syringe()
            .UsingSourceGen()
            .BuildServiceProvider(_configuration);

        var service = provider.GetService<ISyringeTestService>();
        Assert.IsType<SyringeTestService>(service);
        Assert.Same(service, provider.GetService<ISyringeTestService>());
        Assert.Same(_configuration, provider.GetService<IConfiguration>());
    }

    [Fact]
    public void BuildServiceProvider_WithExplicitTypeRegistrar_RunsRegistrarAndResolvesItsRegistrations()
    {
        var typeRegistrar = new RecordingTypeRegistrar();
        var assemblyProvider = new RecordingAssemblyProvider([typeof(ConfiguredSyringeBuildServiceProviderTests).Assembly]);

        var provider = new ConfiguredSyringe()
            .UsingTypeRegistrar(typeRegistrar)
            .UsingPluginFactory(new NoOpPluginFactory())
            .UsingAssemblyProvider(assemblyProvider)
            .UsingServiceProviderBuilderFactory((p, a, extra) => new RecordingServiceProviderBuilder(p, a, extra))
            .BuildServiceProvider(_configuration);

        Assert.IsType<SyringeTestService>(provider.GetService<ISyringeTestService>());
        Assert.IsType<EmptyTypeFilterer>(typeRegistrar.ObservedTypeFilterer);
        Assert.Equal(
            [typeof(ConfiguredSyringeBuildServiceProviderTests).Assembly],
            typeRegistrar.ObservedAssemblies.ToArray());
    }

    [Fact]
    public void BuildServiceProvider_InvokesPostCallbacksThenOptionsThenExtensionsThenVerification()
    {
        var invocations = new List<string>();
        object? optionsServices = null;
        object? optionsConfiguration = null;
        object? extensionServices = null;
        object? extensionConfiguration = null;

        SourceGenRegistry.RegisterOptionsRegistrar((services, config) =>
        {
            invocations.Add("options");
            optionsServices = services;
            optionsConfiguration = config;
        });
        SourceGenRegistry.RegisterExtension((services, config) =>
        {
            invocations.Add("extension");
            extensionServices = services;
            extensionConfiguration = config;
        });

        var builder = default(RecordingServiceProviderBuilder);
        var syringe = new ConfiguredSyringe()
            .UsingTypeRegistrar(new NoOpTypeRegistrar())
            .UsingPluginFactory(new NoOpPluginFactory())
            .UsingAssemblyProvider(new RecordingAssemblyProvider([]))
            .UsingServiceProviderBuilderFactory((p, a, extra) =>
            {
                builder = new RecordingServiceProviderBuilder(p, a, extra);
                return builder;
            })
            .UsingPreRegistrationCallback(_ => invocations.Add("pre"))
            .UsingPostPluginRegistrationCallback(services =>
            {
                invocations.Add("post");
                services.AddScoped<ISyringeTestService, SyringeTestService>();
                services.AddSingleton<ISyringeCaptiveSingleton, SyringeCaptiveSingleton>();
            })
            .WithVerification(new VerificationOptions
            {
                LifetimeMismatchBehavior = VerificationBehavior.Warn,
                IssueReporter = _ => invocations.Add("verification")
            });

        syringe.BuildServiceProvider(_configuration);

        Assert.Equal(["pre", "post", "options", "extension", "verification"], invocations);
        Assert.NotNull(builder);
        Assert.Single(builder!.ObservedPreRegistrationCallbacks);
        Assert.Equal(4, builder.ObservedPostPluginRegistrationCallbacks.Count);
        Assert.Same(optionsConfiguration, _configuration);
        Assert.Same(extensionConfiguration, _configuration);
        Assert.Same(optionsServices, extensionServices);
    }

    [Fact]
    public void BuildServiceProvider_WithoutSourceGenRegistrars_OnlyAppendsVerificationCallback()
    {
        var builder = default(RecordingServiceProviderBuilder);

        new ConfiguredSyringe()
            .UsingTypeRegistrar(new NoOpTypeRegistrar())
            .UsingPluginFactory(new NoOpPluginFactory())
            .UsingAssemblyProvider(new RecordingAssemblyProvider([]))
            .UsingServiceProviderBuilderFactory((p, a, extra) =>
            {
                builder = new RecordingServiceProviderBuilder(p, a, extra);
                return builder;
            })
            .BuildServiceProvider(_configuration);

        Assert.NotNull(builder);
        Assert.Empty(builder!.ObservedPreRegistrationCallbacks);
        Assert.Single(builder.ObservedPostPluginRegistrationCallbacks);
    }

    [Fact]
    public void BuildServiceProvider_StrictVerificationWithLifetimeMismatch_ThrowsContainerVerificationException()
    {
        var syringe = CreateSyringeWithLifetimeMismatch()
            .WithVerification(VerificationOptions.Strict);

        var exception = Assert.Throws<ContainerVerificationException>(
            () => syringe.BuildServiceProvider(_configuration));

        var issue = Assert.Single(exception.Issues);
        Assert.Equal(VerificationIssueType.LifetimeMismatch, issue.Type);
        Assert.Equal(
            [typeof(ISyringeCaptiveSingleton), typeof(ISyringeTestService)],
            issue.InvolvedTypes.ToArray());
    }

    [Fact]
    public void BuildServiceProvider_DisabledVerificationWithLifetimeMismatch_BuildsWithoutReportingIssues()
    {
        var reported = new List<VerificationIssue>();
        var syringe = CreateSyringeWithLifetimeMismatch()
            .WithVerification(VerificationOptions.Disabled with { IssueReporter = reported.Add });

        var provider = syringe.BuildServiceProvider(_configuration);

        Assert.Empty(reported);
        Assert.IsType<SyringeCaptiveSingleton>(provider.GetService<ISyringeCaptiveSingleton>());
    }

    [Fact]
    public void BuildServiceProvider_DefaultVerificationWithLifetimeMismatch_ReportsWarningWithoutThrowing()
    {
        var reported = new List<VerificationIssue>();
        var syringe = CreateSyringeWithLifetimeMismatch()
            .WithVerification(new VerificationOptions { IssueReporter = reported.Add });

        var provider = syringe.BuildServiceProvider(_configuration);

        var issue = Assert.Single(reported);
        Assert.Equal(VerificationBehavior.Warn, issue.ConfiguredBehavior);
        Assert.NotNull(provider.GetService<ISyringeCaptiveSingleton>());
    }

    private static ConfiguredSyringe CreateSyringeWithLifetimeMismatch()
    {
        return new ConfiguredSyringe()
            .UsingTypeRegistrar(new NoOpTypeRegistrar())
            .UsingPluginFactory(new NoOpPluginFactory())
            .UsingAssemblyProvider(new RecordingAssemblyProvider([]))
            .UsingServiceProviderBuilderFactory((p, a, extra) => new RecordingServiceProviderBuilder(p, a, extra))
            .UsingPostPluginRegistrationCallback(services =>
            {
                services.AddScoped<ISyringeTestService, SyringeTestService>();
                services.AddSingleton<ISyringeCaptiveSingleton, SyringeCaptiveSingleton>();
            });
    }
}
