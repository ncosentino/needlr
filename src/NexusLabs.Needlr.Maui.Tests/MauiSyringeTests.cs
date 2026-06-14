using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Hosting;

using NexusLabs.Needlr.Injection;
using NexusLabs.Needlr.Injection.SourceGen;

using Xunit;

namespace NexusLabs.Needlr.Maui.Tests;

/// <summary>
/// Verifies that the MAUI integration applies Needlr-discovered registrations to a real
/// <see cref="MauiAppBuilder"/> and that they resolve from the resulting container. These tests run
/// headless on <c>net10.0</c> and do not require the MAUI workload.
/// </summary>
public sealed class MauiSyringeTests
{
    [Fact]
    public void PopulateInto_RegistersDiscoveredServices_ResolvableFromContainer()
    {
        var builder = MauiApp.CreateBuilder();

        new Syringe()
            .UsingSourceGen()
            .ForMaui()
            .PopulateInto(builder);

        using var provider = builder.Services.BuildServiceProvider();

        var greeter = provider.GetService<ITestGreeter>();
        Assert.NotNull(greeter);
        Assert.IsType<TestGreeter>(greeter);
        Assert.Equal("hello from needlr maui", greeter!.Greet());
    }

    [Fact]
    public void UseNeedlr_WithConfigureCallback_PopulatesDiscoveredServices()
    {
        var builder = MauiApp.CreateBuilder();

        builder.UseNeedlr(syringe => syringe.UsingSourceGen());

        using var provider = builder.Services.BuildServiceProvider();

        Assert.NotNull(provider.GetService<ITestGreeter>());
    }

    [Fact]
    public void PopulateInto_ReturnsSameBuilder_AndPreservesExistingRegistrations()
    {
        var builder = MauiApp.CreateBuilder();
        var marker = new TestMarker();
        builder.Services.AddSingleton(marker);

        var result = new Syringe()
            .UsingSourceGen()
            .ForMaui()
            .PopulateInto(builder);

        Assert.Same(builder, result);

        using var provider = builder.Services.BuildServiceProvider();
        Assert.Same(marker, provider.GetRequiredService<TestMarker>());
        Assert.NotNull(provider.GetService<ITestGreeter>());
    }
}
