using Microsoft.Extensions.DependencyInjection;
using NexusLabs.Needlr.Injection;
using NexusLabs.Needlr.Injection.SourceGen;
using MultiProjectApp.Features.Notifications;
using MultiProjectApp.Features.Reporting;
using Xunit;

namespace MultiProjectApp.ConsoleApp.Tests;

public sealed class BootstrapIntegrationTests
{
    [Fact]
    public void AllPlugins_AreRegistered_WhenBootstrapProjectIsReferenced()
    {
        // ConsoleApp and its transitive references (Bootstrap, Notifications, Reporting) each
        // have a source-generated TypeRegistry. Their module initializers have already fired
        // by the time this test runs, registering all types into NeedlrSourceGenBootstrap.
        var provider = new Syringe()
            .UsingSourceGen()
            .BuildServiceProvider();

        Assert.NotNull(provider.GetRequiredService<INotificationService>());
        Assert.NotNull(provider.GetRequiredService<IReportService>());
    }
}
