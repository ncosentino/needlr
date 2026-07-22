using System.Linq;

using Microsoft.AspNetCore.SignalR;

using NexusLabs.Needlr.Generators;

using Xunit;

namespace NexusLabs.Needlr.SignalR.Tests;

/// <summary>
/// Regression coverage proving <c>NexusLabs.Needlr.SignalR.Generators.SignalRHubRegistryGenerator</c>
/// does not treat an <see cref="IHubRegistrationPlugin"/> implementer using a generated
/// constructor (<c>[GenerateConstructor]</c>) as parameterless. Hub-registration plugins
/// fundamentally require parameterless activation, so this combination must be excluded
/// from the generated <c>SignalRHubRegistry.Entries</c>/<c>MapGeneratedHubs()</c> output
/// rather than emitting a registration for a plugin type that can never actually be
/// constructed without arguments.
/// </summary>
public sealed class SignalRGeneratedConstructorRegistrationTests
{
    [Fact]
    public void GenerateConstructorHubPlugin_IsExcludedFromGeneratedSignalRRegistry()
    {
        var registryType = typeof(GeneratedCtorChatHub).Assembly
            .GetType("NexusLabs.Needlr.SignalR.Tests.Generated.SignalRHubRegistry");
        Assert.NotNull(registryType);

        var entriesProperty = registryType.GetProperty("Entries");
        Assert.NotNull(entriesProperty);

        var entries = entriesProperty.GetValue(null) as System.Collections.Generic.IReadOnlyList<(Type PluginType, Type HubType, string Path)>;
        Assert.NotNull(entries);

        Assert.DoesNotContain(entries, e => e.HubType == typeof(GeneratedCtorChatHub));
        Assert.DoesNotContain(entries, e => e.Path == "/generated-ctor-chat");
    }

    [Fact]
    public void OrdinaryHubPlugin_StillIncludedInGeneratedSignalRRegistry()
    {
        var registryType = typeof(GeneratedCtorChatHub).Assembly
            .GetType("NexusLabs.Needlr.SignalR.Tests.Generated.SignalRHubRegistry");
        Assert.NotNull(registryType);

        var entriesProperty = registryType.GetProperty("Entries");
        Assert.NotNull(entriesProperty);

        var entries = entriesProperty.GetValue(null) as System.Collections.Generic.IReadOnlyList<(Type PluginType, Type HubType, string Path)>;
        Assert.NotNull(entries);

        Assert.Equal(1, entries.Count(e => e.Path == "/chat"));
    }
}

public sealed class GeneratedCtorChatHub : Hub
{
}

/// <summary>
/// A dependency required by <see cref="GeneratedCtorHubRegistration"/>'s generated
/// constructor, making the plugin's effective constructor non-parameterless.
/// </summary>
public interface IGeneratedCtorHubDependency
{
}

public sealed class GeneratedCtorHubDependency : IGeneratedCtorHubDependency
{
}

/// <summary>
/// An <see cref="IHubRegistrationPlugin"/> whose real constructor is generated from a
/// required field, rather than parameterless. The main generator's plugin discovery
/// already excludes this from <c>GetPluginTypes()</c>; this type instead exercises the
/// SignalR generator's own independent registry.
/// </summary>
[GenerateConstructor]
public partial class GeneratedCtorHubRegistration : IHubRegistrationPlugin
{
    private readonly IGeneratedCtorHubDependency _dependency;

    public string HubPath => "/generated-ctor-chat";

    public Type HubType => typeof(GeneratedCtorChatHub);

    public IGeneratedCtorHubDependency Dependency => _dependency;
}
