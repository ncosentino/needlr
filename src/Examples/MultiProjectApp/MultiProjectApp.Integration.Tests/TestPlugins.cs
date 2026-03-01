using Microsoft.Extensions.DependencyInjection;
using MultiProjectApp.Features.Notifications;
using NexusLabs.Needlr;

namespace MultiProjectApp.Integration.Tests;

/// <summary>
/// Test-only interface for capturing notifications sent during a test.
/// This type only exists in the test assembly — it is registered by <see cref="TestInfrastructurePlugin"/>.
/// </summary>
public interface ITestNotificationCapture
{
    IReadOnlyList<(string Recipient, string Message)> Sent { get; }
    void Record(string recipient, string message);
}

/// <summary>
/// In-memory notification capture for assertions in integration tests.
/// Also implements <see cref="INotificationService"/> so it can intercept real sends.
/// </summary>
public sealed class FakeNotificationService : INotificationService, ITestNotificationCapture
{
    private readonly List<(string Recipient, string Message)> _sent = [];

    public IReadOnlyList<(string Recipient, string Message)> Sent => _sent;

    public void Record(string recipient, string message) => _sent.Add((recipient, message));

    public void Send(string recipient, string message) => Record(recipient, message);
}

/// <summary>
/// Test-only plugin discovered via source generation in this test assembly.
/// Registers <see cref="FakeNotificationService"/> as both <see cref="INotificationService"/>
/// (replacing the real one) and <see cref="ITestNotificationCapture"/> (test-only interface).
/// <para>
/// This plugin is discovered because NeedlrAutoGenerate is not overridden to false in this
/// test project, so the generator produces a TypeRegistry for this assembly. When the Syringe
/// builds, TypeRegistries from all loaded assemblies are combined — including this test one.
/// </para>
/// </summary>
public sealed class TestInfrastructurePlugin : IServiceCollectionPlugin
{
    public void Configure(ServiceCollectionPluginOptions options)
    {
        var fake = new FakeNotificationService();
        options.Services.AddSingleton<ITestNotificationCapture>(fake);
        // Replace so this wins regardless of whether NotificationsPlugin ran first or after.
        options.Services.AddSingleton<INotificationService>(fake);
    }
}
