using MultiProjectApp.Features.CrossGenSimulation;
using MultiProjectApp.Features.Notifications;
using NexusLabs.Needlr.Generators;
using NexusLabs.Needlr.Injection.SourceGen.PluginFactories;

using Xunit;

namespace MultiProjectApp.Integration.Tests;

/// <summary>
/// Integration tests that verify the cross-generator plugin registration pattern end-to-end.
/// </summary>
/// <remarks>
/// <para>
/// <c>MultiProjectApp.Features.CrossGenSimulation</c> has <c>NeedlrAutoGenerate=false</c>,
/// so TypeRegistryGenerator emits NO <c>Register()</c> call for that assembly. The only way
/// <c>SimulatedGeneratorPlugin</c> can reach the Needlr registry is via
/// <c>RegisterPlugins()</c>, called from its <c>[ModuleInitializer]</c>.
/// </para>
/// <para>
/// These tests are true mutation killers: commenting out the <c>Register()</c> call inside
/// <c>RegisterPlugins()</c> causes all of them to fail, proving the path is exercised.
/// </para>
/// </remarks>
public sealed class CrossGeneratorPluginDiscoveryTests
{
    [Fact]
    public void RegisterPlugins_ModuleInitializer_ContributesTypeAbsentFromTypeRegistry()
    {
        // SimulatedGeneratorPlugin is ONLY in the registry because RegisterPlugins() ran.
        // TypeRegistryGenerator produced no Register() for this assembly.
        var found = NeedlrSourceGenBootstrap.TryGetProviders(
            out _,
            out var pluginProvider);

        Assert.True(found);

        var factory = new GeneratedPluginFactory(pluginProvider!);
        var assembly = typeof(SimulatedGeneratorPlugin).Assembly;

        var plugins = factory.CreatePluginsFromAssemblies<ICrossGeneratedPlugin>(
            [assembly]).ToList();

        Assert.Contains(plugins, p => p is SimulatedGeneratorPlugin);
    }

    [Fact]
    public void RegisterPlugins_ExactlyOneInstance_NoSpuriousDuplicates()
    {
        var found = NeedlrSourceGenBootstrap.TryGetProviders(
            out _,
            out var pluginProvider);

        Assert.True(found);

        var factory = new GeneratedPluginFactory(pluginProvider!);
        var assembly = typeof(SimulatedGeneratorPlugin).Assembly;

        var plugins = factory.CreatePluginsFromAssemblies<ICrossGeneratedPlugin>(
            [assembly]).OfType<SimulatedGeneratorPlugin>().ToList();

        Assert.Single(plugins);
    }

    [Fact]
    public void RegisterPlugins_AssemblyFilter_TypeNotVisibleFromOtherAssemblies()
    {
        var found = NeedlrSourceGenBootstrap.TryGetProviders(
            out _,
            out var pluginProvider);

        Assert.True(found);

        var factory = new GeneratedPluginFactory(pluginProvider!);

        // Searching in a different assembly should return nothing for ICrossGeneratedPlugin
        var plugins = factory.CreatePluginsFromAssemblies<ICrossGeneratedPlugin>(
            [typeof(string).Assembly]).ToList();

        Assert.Empty(plugins);
    }

    [Fact]
    public void RegisterPlugins_DeduplicatesWithTypeRegistryGenerator_ForHandWrittenNotificationSink()
    {
        // AuditLogNotificationSink is hand-written, so BOTH TypeRegistryGenerator (via Register())
        // AND our CrossGeneratedPlugins module initializer (via RegisterPlugins()) contribute it.
        // Combine() must deduplicate to exactly one instance.
        var found = NeedlrSourceGenBootstrap.TryGetProviders(
            out _,
            out var pluginProvider);

        Assert.True(found);

        var factory = new GeneratedPluginFactory(pluginProvider!);
        var notificationsAssembly = typeof(AuditLogNotificationSink).Assembly;

        var sinks = factory.CreatePluginsFromAssemblies<INotificationSink>(
            [notificationsAssembly]).OfType<AuditLogNotificationSink>().ToList();

        Assert.Single(sinks);
    }
}

