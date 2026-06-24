using System.Linq;
using System.Net;
using System.Threading.Tasks;

using Carter;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

using NexusLabs.Needlr.AspNet;
using NexusLabs.Needlr.Injection;
using NexusLabs.Needlr.Injection.SourceGen;

using Xunit;

#pragma warning disable xUnit1051 // TestContext.Current.CancellationToken not used - not applicable for integration tests

namespace NexusLabs.Needlr.Carter.IntegrationTests;

/// <summary>
/// Exercises Carter modules through the full
/// <c>Syringe().UsingSourceGen().ForWebApplication()…BuildWebApplication()</c> composition —
/// the path on which the duplicate-registration bug occurred and which the existing
/// <c>NexusLabs.Needlr.Carter.Tests</c> never covered.
/// </summary>
/// <remarks>
/// A <c>public</c> module is discovered by both Carter's scan and Needlr's type registry; a
/// gated observe-and-complement keeps it registered exactly once. An <c>internal</c> module is
/// invisible to Carter 10.0.0, so Needlr must still register it. Both must serve their routes
/// without an <see cref="Microsoft.AspNetCore.Routing.Matching.AmbiguousMatchException"/>.
/// </remarks>
public sealed class CarterModuleRegistrationSyringeTests
{
    [Fact]
    public async Task PublicModule_ThroughSyringeSourceGen_RegistersExactlyOnce_AndServesRoute()
    {
        await using var app = BuildWebApplication();
        await app.StartAsync();

        var publicModules = app.Services.GetServices<ICarterModule>().Count(m => m is PublicPingModule);
        Assert.Equal(1, publicModules);

        var response = await app.GetTestClient().GetAsync("/api/public-ping");
        response.EnsureSuccessStatusCode();
        Assert.Contains("public-pong", await response.Content.ReadAsStringAsync());

        await app.StopAsync();
    }

    [Fact]
    public async Task InternalModule_ThroughSyringeSourceGen_RegistersExactlyOnce_AndServesRoute()
    {
        await using var app = BuildWebApplication();
        await app.StartAsync();

        var internalModules = app.Services.GetServices<ICarterModule>().Count(m => m is InternalPingModule);
        Assert.Equal(1, internalModules);

        var response = await app.GetTestClient().GetAsync("/api/internal-ping");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("internal-pong", await response.Content.ReadAsStringAsync());

        await app.StopAsync();
    }

    private static WebApplication BuildWebApplication()
        => new Syringe()
            .UsingSourceGen()
            .ForWebApplication()
            .UsingConfigurationCallback((builder, _) => builder.WebHost.UseTestServer())
            .BuildWebApplication();
}
