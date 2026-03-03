using Microsoft.Extensions.DependencyInjection;
using MultiProjectApp.Features.Notifications;
using NexusLabs.Needlr.Injection;
using NexusLabs.Needlr.Injection.SourceGen;
using Xunit;

namespace MultiProjectApp.WorkerApp.Tests;

/// <summary>
/// Integration tests demonstrating the AssemblyLoader pattern.
/// </summary>
/// <remarks>
/// <para>
/// Unlike <c>MultiProjectApp.ConsoleApp.Tests</c>, this project does NOT reference any
/// <c>MultiProjectApp.WorkerApp</c> or <c>MultiProjectApp.Features.Notifications</c> types
/// directly in test code (the <c>using</c> directives above are only for type-checking the
/// resolved service — the service is obtained purely through the DI container).
/// </para>
/// <para>
/// Without <see cref="AssemblyLoader"/>, <c>WorkerApp.dll</c> would never be loaded,
/// Needlr's source-gen registry would be empty, and <c>GetRequiredService</c> would throw.
/// With it, <c>WorkerApp</c> and its transitive <c>Notifications</c> dependency are both
/// loaded at startup, their module initializers fire, and all types are correctly registered.
/// </para>
/// </remarks>
public sealed class WorkerAppIntegrationTests
{
    [Fact]
    public void NotificationService_IsRegistered_EvenThoughNoTestCodeReferencesItDirectly()
    {
        // Build the service provider using only source-gen discovery.
        // AssemblyLoader.cs has already run (module initializer), ensuring WorkerApp and
        // its Notifications dependency are loaded. No test code here references
        // INotificationService or NotificationWorker directly — we only resolve via DI.
        var provider = new Syringe()
            .UsingSourceGen()
            .BuildServiceProvider();

        // If AssemblyLoader.cs were removed, this would throw because the TypeRegistry
        // for Notifications would never have been registered.
        var service = provider.GetRequiredService<INotificationService>();
        Assert.NotNull(service);
    }
}
