using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using MultiProjectApp.Features.Reporting;
using NexusLabs.Needlr;
using Xunit;

namespace MultiProjectApp.Features.Reporting.Tests;

public sealed class ReportingPluginTests
{
    [Fact]
    public void ReportingPlugin_RegistersReportService()
    {
        var services = new ServiceCollection();
        var plugin = new ReportingPlugin();
        plugin.Configure(new ServiceCollectionPluginOptions(
            services,
            new ConfigurationBuilder().Build(),
            Array.Empty<Assembly>(),
            new Mock<IPluginFactory>().Object));

        var provider = services.BuildServiceProvider();

        Assert.NotNull(provider.GetRequiredService<IReportService>());
    }
}
