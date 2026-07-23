using System.Reflection;

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
/// directly before building the service provider. The test resolves the notification service
/// by assembly and type name only after source-generated registration has completed.
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
        var provider = new Syringe()
            .UsingSourceGen()
            .BuildServiceProvider();

        var notificationsAssembly = Assembly.Load("MultiProjectApp.Features.Notifications");
        var serviceType = notificationsAssembly.GetType(
            "MultiProjectApp.Features.Notifications.INotificationService");
        Assert.NotNull(serviceType);

        var service = provider.GetService(serviceType);
        Assert.NotNull(service);
        Assert.Equal(
            "MultiProjectApp.Features.Notifications.InMemoryNotificationService",
            service.GetType().FullName);
    }
}
