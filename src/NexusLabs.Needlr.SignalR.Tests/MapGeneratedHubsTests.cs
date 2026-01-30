using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;

using Xunit;

namespace NexusLabs.Needlr.SignalR.Tests;

/// <summary>
/// Tests for the source-generated MapGeneratedHubs extension method.
/// </summary>
public sealed class MapGeneratedHubsTests
{
    [Fact]
    public void MapGeneratedHubs_ExtensionMethodExists()
    {
        // The source generator should create a MapGeneratedHubs extension method
        // in the {AssemblyName}.Generated namespace
        var extensionsType = typeof(TestChatHubRegistration).Assembly
            .GetType("NexusLabs.Needlr.SignalR.Tests.Generated.SignalRHubExtensions");

        Assert.NotNull(extensionsType);

        var mapMethod = extensionsType.GetMethod("MapGeneratedHubs");
        Assert.NotNull(mapMethod);
    }

    [Fact]
    public void MapGeneratedHubs_ReturnsWebApplication()
    {
        var extensionsType = typeof(TestChatHubRegistration).Assembly
            .GetType("NexusLabs.Needlr.SignalR.Tests.Generated.SignalRHubExtensions");
        Assert.NotNull(extensionsType);

        var mapMethod = extensionsType.GetMethod("MapGeneratedHubs");
        Assert.NotNull(mapMethod);

        // Should return WebApplication for chaining
        Assert.Equal(typeof(WebApplication), mapMethod.ReturnType);
    }

    [Fact]
    public void MapGeneratedHubs_AcceptsWebApplicationParameter()
    {
        var extensionsType = typeof(TestChatHubRegistration).Assembly
            .GetType("NexusLabs.Needlr.SignalR.Tests.Generated.SignalRHubExtensions");
        Assert.NotNull(extensionsType);

        var mapMethod = extensionsType.GetMethod("MapGeneratedHubs");
        Assert.NotNull(mapMethod);

        var parameters = mapMethod.GetParameters();
        Assert.Single(parameters);
        Assert.Equal(typeof(WebApplication), parameters[0].ParameterType);
    }

    [Fact]
    public void MapGeneratedHubs_CanBeInvokedOnWebApplication()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddSignalR();
        var app = builder.Build();

        // Call the generated MapGeneratedHubs method via reflection
        var extensionsType = typeof(TestChatHubRegistration).Assembly
            .GetType("NexusLabs.Needlr.SignalR.Tests.Generated.SignalRHubExtensions");
        Assert.NotNull(extensionsType);

        var mapMethod = extensionsType.GetMethod("MapGeneratedHubs");
        Assert.NotNull(mapMethod);

        var exception = Record.Exception(() => mapMethod.Invoke(null, new object[] { app }));
        Assert.Null(exception);
    }

    [Fact]
    public void MapGeneratedHubs_MapsAllDiscoveredHubs()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddSignalR();
        var app = builder.Build();

        // Get the registry to verify expected hubs
        var registryType = typeof(TestChatHubRegistration).Assembly
            .GetType("NexusLabs.Needlr.SignalR.Tests.Generated.SignalRHubRegistry");
        Assert.NotNull(registryType);

        var entriesProperty = registryType.GetProperty("Entries");
        Assert.NotNull(entriesProperty);

        var entries = entriesProperty.GetValue(null) as System.Collections.IList;
        Assert.NotNull(entries);
        Assert.True(entries.Count >= 2, $"Expected at least 2 hub registrations but found {entries.Count}");
    }

    [Fact]
    public void SignalRHubRegistry_ContainsExpectedHubPaths()
    {
        var registryType = typeof(TestChatHubRegistration).Assembly
            .GetType("NexusLabs.Needlr.SignalR.Tests.Generated.SignalRHubRegistry");
        Assert.NotNull(registryType);

        var entriesProperty = registryType.GetProperty("Entries");
        Assert.NotNull(entriesProperty);

        var entries = entriesProperty.GetValue(null) as System.Collections.Generic.IReadOnlyList<(Type PluginType, Type HubType, string Path)>;
        Assert.NotNull(entries);

        var paths = entries.Select(e => e.Path).ToList();
        Assert.Contains("/chat", paths);
        Assert.Contains("/notifications", paths);
    }

    [Fact]
    public void SignalRHubRegistry_ContainsExpectedHubTypes()
    {
        var registryType = typeof(TestChatHubRegistration).Assembly
            .GetType("NexusLabs.Needlr.SignalR.Tests.Generated.SignalRHubRegistry");
        Assert.NotNull(registryType);

        var entriesProperty = registryType.GetProperty("Entries");
        Assert.NotNull(entriesProperty);

        var entries = entriesProperty.GetValue(null) as System.Collections.Generic.IReadOnlyList<(Type PluginType, Type HubType, string Path)>;
        Assert.NotNull(entries);

        var hubTypes = entries.Select(e => e.HubType).ToList();
        Assert.Contains(typeof(TestChatHub), hubTypes);
        Assert.Contains(typeof(TestNotificationHub), hubTypes);
    }
}
