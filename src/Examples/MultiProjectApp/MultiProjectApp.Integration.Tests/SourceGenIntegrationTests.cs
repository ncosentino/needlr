using Microsoft.Extensions.DependencyInjection;
using MultiProjectApp.Features.Notifications;
using MultiProjectApp.Features.Reporting;
using NexusLabs.Needlr.Injection;
using NexusLabs.Needlr.Injection.SourceGen;
using Xunit;

namespace MultiProjectApp.Integration.Tests;

/// <summary>
/// Integration tests demonstrating a test project that participates in source generation.
/// <para>
/// Because NeedlrAutoGenerate is not set to false here, the generator produces a TypeRegistry
/// for this assembly. When the Syringe builds, TypeRegistries from all loaded assemblies
/// (Bootstrap's feature references + this test assembly) are combined automatically.
/// </para>
/// <para>
/// This means test-only plugin types like <see cref="TestInfrastructurePlugin"/> are
/// discovered and run alongside the real feature plugins — no manual wiring needed.
/// </para>
/// </summary>
public sealed class SourceGenIntegrationTests
{
    [Fact]
    public void TestInfrastructurePlugin_IsDiscoveredBySourceGen_AndTestCaptureIsResolvable()
    {
        var provider = new Syringe()
            .UsingSourceGen()
            .BuildServiceProvider();

        // IReportService comes from the real ReportingPlugin (feature assembly)
        Assert.NotNull(provider.GetRequiredService<IReportService>());

        // ITestNotificationCapture is a test-only interface — only registered by
        // TestInfrastructurePlugin in this test assembly's TypeRegistry.
        // If this resolves, it proves the test assembly's source-generated TypeRegistry
        // was discovered and its plugin was executed.
        Assert.NotNull(provider.GetRequiredService<ITestNotificationCapture>());
    }

    [Fact]
    public void FakeNotificationService_CapturesSentNotifications()
    {
        var provider = new Syringe()
            .UsingSourceGen()
            .BuildServiceProvider();

        // Resolve via the test-only capture interface
        var capture = provider.GetRequiredService<ITestNotificationCapture>();
        Assert.IsType<FakeNotificationService>(capture);

        capture.Record("alice@example.com", "Hello from test");

        Assert.Single(capture.Sent);
        Assert.Equal("alice@example.com", capture.Sent[0].Recipient);
        Assert.Equal("Hello from test", capture.Sent[0].Message);
    }
}
