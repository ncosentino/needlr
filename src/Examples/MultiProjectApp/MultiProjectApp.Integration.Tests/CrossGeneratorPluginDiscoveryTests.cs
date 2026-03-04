using MultiProjectApp.Features.Notifications;
using NexusLabs.Needlr.Generators;
using NexusLabs.Needlr.Injection.SourceGen.PluginFactories;

using Xunit;

namespace MultiProjectApp.Integration.Tests;

/// <summary>
/// Integration test that verifies the cross-generator plugin registration pattern end-to-end.
/// </summary>
/// <remarks>
/// <para>
/// This test proves that <c>NotificationSinkRegistrations.Initialize()</c> (the
/// <c>[ModuleInitializer]</c> in <c>MultiProjectApp.Features.Notifications</c>) has already
/// run by the time this test executes, and that <c>AuditLogNotificationSink</c> is
/// discoverable via <c>CreatePluginsFromAssemblies&lt;INotificationSink&gt;()</c>.
/// </para>
/// <para>
/// The <c>MultiProjectApp.Features.Notifications</c> assembly also has a TypeRegistryGenerator-
/// emitted module initializer that calls <c>Register()</c> with the same type. This test
/// verifies that <c>NeedlrSourceGenBootstrap.Combine()</c> deduplicates correctly and returns
/// exactly one <c>AuditLogNotificationSink</c> instance.
/// </para>
/// </remarks>
public sealed class CrossGeneratorPluginDiscoveryTests
{
    [Fact]
    public void RegisterPlugins_ModuleInitializer_ContributesNotificationSink_ToMergedProvider()
    {
        var found = NeedlrSourceGenBootstrap.TryGetProviders(
            out _,
            out var pluginProvider);

        Assert.True(found, "Expected at least one assembly to have called Register() or RegisterPlugins().");

        var factory = new GeneratedPluginFactory(pluginProvider!);
        var notificationsAssembly = typeof(AuditLogNotificationSink).Assembly;

        var sinks = factory.CreatePluginsFromAssemblies<INotificationSink>(
            [notificationsAssembly]).ToList();

        Assert.Contains(sinks, s => s is AuditLogNotificationSink);
    }

    [Fact]
    public void RegisterPlugins_DeduplicatesWithTypeRegistryGenerator_ReturnsExactlyOneInstance()
    {
        var found = NeedlrSourceGenBootstrap.TryGetProviders(
            out _,
            out var pluginProvider);

        Assert.True(found);

        var factory = new GeneratedPluginFactory(pluginProvider!);
        var notificationsAssembly = typeof(AuditLogNotificationSink).Assembly;

        var sinks = factory.CreatePluginsFromAssemblies<INotificationSink>(
            [notificationsAssembly]).ToList();

        var auditSinks = sinks.OfType<AuditLogNotificationSink>().ToList();
        Assert.Single(auditSinks);
    }
}
