using System.Reflection;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;

using NexusLabs.Needlr.AspNet;
using NexusLabs.Needlr.Generators;
using NexusLabs.Needlr.Injection.Reflection.PluginFactories;
using NexusLabs.Needlr.Injection.SourceGen.PluginFactories;

using Xunit;

namespace NexusLabs.Needlr.SignalR.Tests;

/// <summary>
/// Integration tests verifying SignalR plugin discovery works with both
/// reflection and source generation paths.
/// </summary>
public sealed class SourceGenIntegrationTests
{
    [Fact]
    public void SignalR_PackageHasOwnTypeRegistry()
    {
        var signalRAssembly = typeof(SignalRWebApplicationBuilderPlugin).Assembly;
        var typeRegistryType = signalRAssembly.GetType("NexusLabs.Needlr.SignalR.Generated.TypeRegistry");

        Assert.NotNull(typeRegistryType);
        var getPluginTypesMethod = typeRegistryType.GetMethod("GetPluginTypes");
        Assert.NotNull(getPluginTypesMethod);
    }

    [Fact]
    public void SignalR_PackageHasModuleInitializer()
    {
        var signalRAssembly = typeof(SignalRWebApplicationBuilderPlugin).Assembly;
        var moduleInitializerType = signalRAssembly.GetType("NexusLabs.Needlr.SignalR.Generated.NeedlrSourceGenModuleInitializer");

        Assert.NotNull(moduleInitializerType);
    }

    [Fact]
    public void SignalR_PluginsRegisteredViaOwnTypeRegistry()
    {
        var signalRAssembly = typeof(SignalRWebApplicationBuilderPlugin).Assembly;
        var typeRegistryType = signalRAssembly.GetType("NexusLabs.Needlr.SignalR.Generated.TypeRegistry");
        Assert.NotNull(typeRegistryType);

        var getPluginTypesMethod = typeRegistryType.GetMethod("GetPluginTypes");
        Assert.NotNull(getPluginTypesMethod);

        var pluginTypes = (IReadOnlyList<PluginTypeInfo>)getPluginTypesMethod.Invoke(null, null)!;
        var pluginTypeNames = pluginTypes.Select(p => p.PluginType.Name).ToList();
        
        Assert.Contains(pluginTypeNames, n => n == "SignalRWebApplicationBuilderPlugin");
    }

    [Fact]
    public void PluginParity_IWebApplicationBuilderPlugin_BothFactoriesDiscoverSamePlugins()
    {
        var signalRAssembly = typeof(SignalRWebApplicationBuilderPlugin).Assembly;
        var assemblies = new[] { signalRAssembly };
        var reflectionFactory = new ReflectionPluginFactory();
        var generatedFactory = new GeneratedPluginFactory(
            SignalR.Generated.TypeRegistry.GetPluginTypes);

        var reflectionPlugins = reflectionFactory
            .CreatePluginsFromAssemblies<IWebApplicationBuilderPlugin>(assemblies)
            .Select(p => p.GetType().FullName)
            .OrderBy(n => n)
            .ToList();

        var generatedPlugins = generatedFactory
            .CreatePluginsFromAssemblies<IWebApplicationBuilderPlugin>(assemblies)
            .Select(p => p.GetType().FullName)
            .OrderBy(n => n)
            .ToList();

        Assert.Equal(reflectionPlugins, generatedPlugins);
        Assert.Contains(reflectionPlugins, n => n == "NexusLabs.Needlr.SignalR.SignalRWebApplicationBuilderPlugin");
    }

    [Fact]
    public void PluginParity_AllPluginTypes_IdenticalBetweenReflectionAndGenerated()
    {
        var signalRAssembly = typeof(SignalRWebApplicationBuilderPlugin).Assembly;
        var assemblies = new[] { signalRAssembly };
        var reflectionFactory = new ReflectionPluginFactory();
        var generatedFactory = new GeneratedPluginFactory(
            SignalR.Generated.TypeRegistry.GetPluginTypes);

        var reflectionPlugins = reflectionFactory
            .CreatePluginsFromAssemblies<IWebApplicationBuilderPlugin>(assemblies)
            .Select(p => p.GetType().FullName)
            .ToList();

        var generatedPlugins = generatedFactory
            .CreatePluginsFromAssemblies<IWebApplicationBuilderPlugin>(assemblies)
            .Select(p => p.GetType().FullName)
            .ToList();

        Assert.Single(reflectionPlugins);
        Assert.Single(generatedPlugins);
        Assert.Equal(reflectionPlugins.OrderBy(x => x), generatedPlugins.OrderBy(x => x));
    }
}

/// <summary>
/// Integration tests verifying real hub types work with the SignalR plugin system.
/// </summary>
public sealed class RealHubIntegrationTests
{
    [Fact]
    public void HubRegistrationPlugin_WithRealHub_CanBeDiscoveredViaReflection()
    {
        var assemblies = new[] { typeof(TestChatHubRegistration).Assembly };
        var reflectionFactory = new ReflectionPluginFactory();

        var plugins = reflectionFactory
            .CreatePluginsFromAssemblies<IHubRegistrationPlugin>(assemblies)
            .ToList();

        Assert.NotEmpty(plugins);
        Assert.Contains(plugins, p => p.GetType() == typeof(TestChatHubRegistration));
        
        var chatPlugin = plugins.First(p => p is TestChatHubRegistration);
        Assert.Equal("/chat", chatPlugin.HubPath);
        Assert.Equal(typeof(TestChatHub), chatPlugin.HubType);
    }

    [Fact]
    public void HubRegistrationPlugin_WithRealHub_CanBeDiscoveredViaSourceGen()
    {
        var assemblies = new[] { typeof(TestChatHubRegistration).Assembly };
        var generatedFactory = new GeneratedPluginFactory(
            NexusLabs.Needlr.SignalR.Tests.Generated.TypeRegistry.GetPluginTypes);

        var plugins = generatedFactory
            .CreatePluginsFromAssemblies<IHubRegistrationPlugin>(assemblies)
            .ToList();

        Assert.NotEmpty(plugins);
        Assert.Contains(plugins, p => p.GetType() == typeof(TestChatHubRegistration));
        
        var chatPlugin = plugins.First(p => p is TestChatHubRegistration);
        Assert.Equal("/chat", chatPlugin.HubPath);
        Assert.Equal(typeof(TestChatHub), chatPlugin.HubType);
    }

    [Fact]
    public void HubRegistrationPlugin_Parity_ReflectionAndSourceGenDiscoverSameHubs()
    {
        var assemblies = new[] { typeof(TestChatHubRegistration).Assembly };
        var reflectionFactory = new ReflectionPluginFactory();
        var generatedFactory = new GeneratedPluginFactory(
            NexusLabs.Needlr.SignalR.Tests.Generated.TypeRegistry.GetPluginTypes);

        var reflectionPlugins = reflectionFactory
            .CreatePluginsFromAssemblies<IHubRegistrationPlugin>(assemblies)
            .Select(p => (Type: p.GetType().FullName, p.HubPath, HubType: p.HubType.FullName))
            .OrderBy(x => x.Type)
            .ToList();

        var generatedPlugins = generatedFactory
            .CreatePluginsFromAssemblies<IHubRegistrationPlugin>(assemblies)
            .Select(p => (Type: p.GetType().FullName, p.HubPath, HubType: p.HubType.FullName))
            .OrderBy(x => x.Type)
            .ToList();

        Assert.Equal(reflectionPlugins.Count, generatedPlugins.Count);
        Assert.Equal(reflectionPlugins, generatedPlugins);
    }

    [Fact]
    public void MultipleHubRegistrations_AllDiscoveredCorrectly()
    {
        var assemblies = new[] { typeof(TestChatHubRegistration).Assembly };
        var reflectionFactory = new ReflectionPluginFactory();

        var plugins = reflectionFactory
            .CreatePluginsFromAssemblies<IHubRegistrationPlugin>(assemblies)
            .ToList();

        // There are 4 hub registrations in the test project (across different test files)
        Assert.True(plugins.Count >= 2, $"Expected at least 2 hub registrations but found {plugins.Count}");
        Assert.Contains(plugins, p => p.HubPath == "/chat");
        Assert.Contains(plugins, p => p.HubPath == "/notifications");
    }

    [Fact]
    public void HubPlugin_CanConfigureWebApplication()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddSignalR();
        var app = builder.Build();

        var plugin = new TestChatHubRegistration();
        
        var exception = Record.Exception(() => app.MapHub<TestChatHub>(plugin.HubPath));
        Assert.Null(exception);
    }
}

public sealed class TestChatHub : Hub
{
    public async Task SendMessage(string user, string message)
    {
        await Clients.All.SendAsync("ReceiveMessage", user, message);
    }
}

public sealed class TestNotificationHub : Hub
{
    public async Task SendNotification(string message)
    {
        await Clients.All.SendAsync("ReceiveNotification", message);
    }
}

public sealed class TestChatHubRegistration : IHubRegistrationPlugin
{
    public string HubPath => "/chat";
    public Type HubType => typeof(TestChatHub);
}

public sealed class TestNotificationHubRegistration : IHubRegistrationPlugin
{
    public string HubPath => "/notifications";
    public Type HubType => typeof(TestNotificationHub);
}
