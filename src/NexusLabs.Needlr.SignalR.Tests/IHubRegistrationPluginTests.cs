using Microsoft.AspNetCore.SignalR;

using Xunit;

namespace NexusLabs.Needlr.SignalR.Tests;

public sealed class IHubRegistrationPluginTests
{
    [Fact]
    public void Implementation_HasCorrectHubPath()
    {
        // Arrange
        var plugin = new TestHubRegistrationPlugin();

        // Act & Assert
        Assert.Equal("/testhub", plugin.HubPath);
    }

    [Fact]
    public void Implementation_HasCorrectHubType()
    {
        // Arrange
        var plugin = new TestHubRegistrationPlugin();

        // Act & Assert
        Assert.Equal(typeof(TestHub), plugin.HubType);
    }
}

/// <summary>
/// Test implementation of IHubRegistrationPlugin for testing.
/// </summary>
public sealed class TestHubRegistrationPlugin : IHubRegistrationPlugin
{
    public string HubPath => "/testhub";
    public Type HubType => typeof(TestHub);
}

public sealed class TestHub : Hub
{
    // Empty hub for testing
}
