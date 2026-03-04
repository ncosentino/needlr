using System.Runtime.CompilerServices;

using NexusLabs.Needlr.Generators;

namespace MultiProjectApp.Features.Notifications;

/// <summary>
/// A sink that receives and processes notifications emitted by the application.
/// </summary>
/// <remarks>
/// This interface exists to demonstrate the <c>RegisterPlugins()</c> cross-generator pattern.
/// In a real scenario a second generator (such as a CacheProviderGenerator) would
/// emit types implementing this interface, and TypeRegistryGenerator would not be able to see
/// them because Roslyn generators operate on the original compilation in isolation.
/// </remarks>
public interface INotificationSink
{
    void OnNotification(string recipient, string message);
}

/// <summary>
/// Writes notifications to the console as an audit trail.
/// </summary>
/// <remarks>
/// In the multi-generator scenario this type would be emitted by a second source generator.
/// TypeRegistryGenerator can see it here because it is hand-written, but the
/// <c>NotificationSinkRegistrations</c> class below demonstrates the pattern a generated
/// <c>[ModuleInitializer]</c> would use.
/// </remarks>
public sealed record AuditLogNotificationSink : INotificationSink
{
    public void OnNotification(string recipient, string message) =>
        Console.WriteLine($"[Audit] {recipient}: {message}");
}

/// <summary>
/// Simulates the module initializer that a second source generator would emit to register
/// its plugin types with Needlr at runtime, bypassing the Roslyn generator isolation boundary.
/// </summary>
/// <remarks>
/// <para>
/// Roslyn source generators run against the original compilation — a generator cannot see types
/// emitted by another generator in the same build. This means <c>TypeRegistryGenerator</c>
/// cannot include types produced by a second generator in the TypeRegistry.
/// </para>
/// <para>
/// The workaround is runtime registration: the second generator emits a
/// <c>[ModuleInitializer]</c> (like the one below) that calls
/// <c>NeedlrSourceGenBootstrap.RegisterPlugins()</c>. All module initializers complete before
/// any user code runs, so the plugins are available by the time the application calls
/// <c>IPluginFactory.CreatePluginsFromAssemblies&lt;T&gt;()</c>.
/// </para>
/// <para>
/// <c>NeedlrSourceGenBootstrap.Combine()</c> deduplicates across all registrations, so if
/// TypeRegistryGenerator also sees the type (as in this example where it is hand-written),
/// only one instance appears in the merged provider.
/// </para>
/// </remarks>
internal static class NotificationSinkRegistrations
{
#pragma warning disable CA2255
    [ModuleInitializer]
    internal static void Initialize()
#pragma warning restore CA2255
    {
        NeedlrSourceGenBootstrap.RegisterPlugins(static () =>
        [
            new PluginTypeInfo(
                typeof(AuditLogNotificationSink),
                [typeof(INotificationSink)],
                static () => new AuditLogNotificationSink(),
                [])
        ]);
    }
}
