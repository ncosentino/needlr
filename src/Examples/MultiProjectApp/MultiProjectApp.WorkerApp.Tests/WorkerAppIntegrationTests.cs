using Microsoft.Extensions.DependencyInjection;
using MultiProjectApp.Features.Notifications;
using NexusLabs.Needlr.Injection;
using NexusLabs.Needlr.Injection.SourceGen;
using Xunit;

namespace MultiProjectApp.WorkerApp.Tests;

/// <summary>
/// Integration tests demonstrating the Needlr cascade loading pattern.
/// </summary>
/// <remarks>
/// <para>
/// Unlike <c>MultiProjectApp.ConsoleApp.Tests</c>, this project does NOT reference any
/// <c>MultiProjectApp.WorkerApp</c> or <c>MultiProjectApp.Features.Notifications</c> types
/// directly in test code (the <c>using</c> directives above are only for type-checking the
/// resolved service — the service is obtained purely through the DI container).
/// </para>
/// <para>
/// Because <c>NexusLabs.Needlr.Build</c> activates the source generator on this project,
/// a <c>NeedlrSourceGenModuleInitializer</c> is generated that calls
/// <c>ForceLoadReferencedAssemblies()</c>, loading <c>WorkerApp.dll</c> and its transitive
/// <c>Notifications</c> dependency at startup. Their module initializers fire, all types
/// are correctly registered, and no manual <c>AssemblyLoader</c> is needed.
/// </para>
/// </remarks>
public sealed class WorkerAppIntegrationTests
{
    [Fact]
    public void NotificationService_IsRegistered_EvenThoughNoTestCodeReferencesItDirectly()
    {
        // Build the service provider using only source-gen discovery.
        // The generated NeedlrSourceGenModuleInitializer.ForceLoadReferencedAssemblies() has
        // already run (module initializer), ensuring WorkerApp and its Notifications dependency
        // are loaded. No test code here references INotificationService or NotificationWorker
        // directly — we only resolve via DI.
        var provider = new Syringe()
            .UsingSourceGen()
            .BuildServiceProvider();

        // If the generator were not running on this project (e.g., NexusLabs.Needlr.Build
        // not applied), this would throw because the TypeRegistry for Notifications would
        // never have been registered.
        var service = provider.GetRequiredService<INotificationService>();
        Assert.NotNull(service);
    }
}
