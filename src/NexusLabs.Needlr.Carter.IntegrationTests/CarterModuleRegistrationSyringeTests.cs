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

namespace NexusLabs.Needlr.Carter.IntegrationTests;

/// <summary>
/// Exercises Carter modules through the full
/// <c>Syringe().UsingSourceGen().ForWebApplication()…BuildWebApplication()</c> composition —
/// the path on which the duplicate-registration bug occurred and which the existing
/// <c>NexusLabs.Needlr.Carter.Tests</c> never covered.
/// </summary>
/// <remarks>
/// The Carter plugin calls <c>AddCarter(c => c.WithEmptyModules())</c>, so Carter does not scan
/// for modules and Needlr's type registry is the sole registrar. A <c>public</c> module would
/// otherwise be discovered by both Carter and Needlr and registered twice; an <c>internal</c>
/// module is invisible to Carter 10.0.0 and relies on Needlr entirely. Both must register exactly
/// once and serve their routes without an
/// <see cref="Microsoft.AspNetCore.Routing.Matching.AmbiguousMatchException"/>.
/// </remarks>
public sealed class CarterModuleRegistrationSyringeTests
{
    [Fact]
    public async Task PublicModule_ThroughSyringeSourceGen_RegistersExactlyOnce_AndServesRoute()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        await using var app = BuildWebApplication();
        await app.StartAsync(cancellationToken);

        var publicModules = app.Services.GetServices<ICarterModule>().Count(m => m is PublicPingModule);
        Assert.Equal(1, publicModules);

        var response = await app.GetTestClient().GetAsync("/api/public-ping", cancellationToken);
        response.EnsureSuccessStatusCode();
        Assert.Contains("public-pong", await response.Content.ReadAsStringAsync(cancellationToken));

        await app.StopAsync(cancellationToken);
    }

    [Fact]
    public async Task InternalModule_ThroughSyringeSourceGen_RegistersExactlyOnce_AndServesRoute()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        await using var app = BuildWebApplication();
        await app.StartAsync(cancellationToken);

        var internalModules = app.Services.GetServices<ICarterModule>().Count(m => m is InternalPingModule);
        Assert.Equal(1, internalModules);

        var response = await app.GetTestClient().GetAsync("/api/internal-ping", cancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("internal-pong", await response.Content.ReadAsStringAsync(cancellationToken));

        await app.StopAsync(cancellationToken);
    }

    private static WebApplication BuildWebApplication()
        => new Syringe()
            .UsingSourceGen()
            .ForWebApplication()
            .UsingConfigurationCallback((builder, _) => builder.WebHost.UseTestServer())
            .BuildWebApplication();
}
