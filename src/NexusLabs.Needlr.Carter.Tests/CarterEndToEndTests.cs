using Carter;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using Xunit;

namespace NexusLabs.Needlr.Carter.Tests;

/// <summary>
/// Baseline end-to-end tests that verify Carter modules handle HTTP requests using
/// Carter's own <c>AddCarter()</c>/<c>MapCarter()</c>. The full Needlr source-gen
/// composition — where Needlr discovers and registers the modules — is covered by
/// <c>NexusLabs.Needlr.Carter.IntegrationTests</c>.
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
        var cancellationToken = TestContext.Current.CancellationToken;
        var host = await CreateTestHostAsync(cancellationToken);
        var client = host.GetTestClient();

        // Act
        var response = await client.GetAsync("/api/test", cancellationToken);

        // Assert
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        Assert.Contains("Hello from Carter!", content);
    }

    [Fact]
    public async Task CarterModule_GetWithParameter_ReturnsOk()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var host = await CreateTestHostAsync(cancellationToken);
        var client = host.GetTestClient();

        // Act
        var response = await client.GetAsync("/api/test/42", cancellationToken);

        // Assert
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        Assert.Contains("42", content);
        Assert.Contains("Got item 42", content);
    }

    [Fact]
    public async Task CarterModule_PostRequest_ReturnsCreated()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var host = await CreateTestHostAsync(cancellationToken);
        var client = host.GetTestClient();
        var request = new TestRequest(123, "Test Item");
        var jsonContent = new StringContent(
            System.Text.Json.JsonSerializer.Serialize(request),
            System.Text.Encoding.UTF8,
            "application/json");

        // Act
        var response = await client.PostAsync("/api/test", jsonContent, cancellationToken);

        // Assert
        Assert.Equal(System.Net.HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("/api/test/123", response.Headers.Location?.ToString());
    }

    [Fact]
    public async Task CarterModule_NonExistentRoute_Returns404()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var host = await CreateTestHostAsync(cancellationToken);
        var client = host.GetTestClient();

        // Act
        var response = await client.GetAsync("/api/nonexistent", cancellationToken);

        // Assert
        Assert.Equal(System.Net.HttpStatusCode.NotFound, response.StatusCode);
    }

    private static async Task<IHost> CreateTestHostAsync(CancellationToken cancellationToken)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();

        // Add Carter directly for baseline testing
        builder.Services.AddCarter();

        var app = builder.Build();

        app.MapCarter();

        await app.StartAsync(cancellationToken);

        return app;
    }
}
