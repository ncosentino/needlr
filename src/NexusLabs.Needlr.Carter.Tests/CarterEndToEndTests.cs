using Carter;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using NexusLabs.Needlr.AspNet;
using NexusLabs.Needlr.Injection;
using NexusLabs.Needlr.Injection.SourceGen.PluginFactories;

using Xunit;

#pragma warning disable xUnit1051 // TestContext.Current.CancellationToken not used - not applicable for integration tests

namespace NexusLabs.Needlr.Carter.Tests;

/// <summary>
/// End-to-end tests that verify Carter modules actually handle HTTP requests
/// when configured through the Needlr plugin system.
/// </summary>
public sealed class CarterEndToEndTests
{
    /// <summary>
    /// A simple Carter module for testing.
    /// </summary>
    public sealed class TestCarterModule : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            app.MapGet("/api/test", () => Results.Ok(new { message = "Hello from Carter!" }));
            app.MapGet("/api/test/{id}", (int id) => Results.Ok(new { id, message = $"Got item {id}" }));
            app.MapPost("/api/test", (TestRequest request) => Results.Created($"/api/test/{request.Id}", request));
        }
    }

    public record TestRequest(int Id, string Name);

    [Fact]
    public async Task CarterModule_GetRequest_ReturnsOk()
    {
        // Arrange
        var host = await CreateTestHostAsync();
        var client = host.GetTestClient();

        // Act
        var response = await client.GetAsync("/api/test");

        // Assert
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("Hello from Carter!", content);
    }

    [Fact]
    public async Task CarterModule_GetWithParameter_ReturnsOk()
    {
        // Arrange
        var host = await CreateTestHostAsync();
        var client = host.GetTestClient();

        // Act
        var response = await client.GetAsync("/api/test/42");

        // Assert
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("42", content);
        Assert.Contains("Got item 42", content);
    }

    [Fact]
    public async Task CarterModule_PostRequest_ReturnsCreated()
    {
        // Arrange
        var host = await CreateTestHostAsync();
        var client = host.GetTestClient();
        var request = new TestRequest(123, "Test Item");
        var jsonContent = new StringContent(
            System.Text.Json.JsonSerializer.Serialize(request),
            System.Text.Encoding.UTF8,
            "application/json");

        // Act
        var response = await client.PostAsync("/api/test", jsonContent);

        // Assert
        Assert.Equal(System.Net.HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("/api/test/123", response.Headers.Location?.ToString());
    }

    [Fact]
    public async Task CarterModule_NonExistentRoute_Returns404()
    {
        // Arrange
        var host = await CreateTestHostAsync();
        var client = host.GetTestClient();

        // Act
        var response = await client.GetAsync("/api/nonexistent");

        // Assert
        Assert.Equal(System.Net.HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CarterModule_ConfiguredViaPluginSystem_WorksEndToEnd()
    {
        // Arrange - use plugin system to configure Carter
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();

        // Configure Carter via the Needlr plugin
        var builderPlugin = new CarterWebApplicationBuilderPlugin();
        var mockLogger = new Moq.Mock<ILogger>();
        var mockPluginFactory = new Moq.Mock<IPluginFactory>();
        var builderOptions = new WebApplicationBuilderPluginOptions(
            builder, [], mockLogger.Object, mockPluginFactory.Object);
        builderPlugin.Configure(builderOptions);

        var app = builder.Build();

        // Configure routes via the Needlr plugin
        var appPlugin = new CarterWebApplicationPlugin();
        var appOptions = new WebApplicationPluginOptions(app, [], mockPluginFactory.Object);
        appPlugin.Configure(appOptions);

        await app.StartAsync();

        var client = app.GetTestClient();

        // Act
        var response = await client.GetAsync("/api/test");

        // Assert
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("Hello from Carter!", content);

        await app.StopAsync();
    }

    [Fact]
    public async Task CarterModule_WithSourceGenPluginFactory_WorksEndToEnd()
    {
        // Arrange - use source-generated plugin factory
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();

        // Use the REAL source-generated plugin factory from Needlr.Carter
        var pluginFactory = new GeneratedPluginFactory(
            Generated.TypeRegistry.GetPluginTypes);

        var mockLogger = new Moq.Mock<ILogger>();
        var builderOptions = new WebApplicationBuilderPluginOptions(
            builder, [typeof(CarterWebApplicationBuilderPlugin).Assembly], mockLogger.Object, pluginFactory);

        // Get plugins via the source-generated factory
        var builderPlugins = pluginFactory.CreatePluginsFromAssemblies<IWebApplicationBuilderPlugin>(
            [typeof(CarterWebApplicationBuilderPlugin).Assembly]);

        foreach (var plugin in builderPlugins)
        {
            plugin.Configure(builderOptions);
        }

        var app = builder.Build();

        var appOptions = new WebApplicationPluginOptions(
            app, [typeof(CarterWebApplicationPlugin).Assembly], pluginFactory);

        var appPlugins = pluginFactory.CreatePluginsFromAssemblies<IWebApplicationPlugin>(
            [typeof(CarterWebApplicationPlugin).Assembly]);

        foreach (var plugin in appPlugins)
        {
            plugin.Configure(appOptions);
        }

        await app.StartAsync();

        var client = app.GetTestClient();

        // Act
        var response = await client.GetAsync("/api/test");

        // Assert
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("Hello from Carter!", content);

        await app.StopAsync();
    }

    private static async Task<IHost> CreateTestHostAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();

        // Add Carter directly for baseline testing
        builder.Services.AddCarter();

        var app = builder.Build();

        app.MapCarter();

        await app.StartAsync();

        return app;
    }
}
