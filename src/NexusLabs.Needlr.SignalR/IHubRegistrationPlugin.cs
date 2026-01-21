namespace NexusLabs.Needlr.SignalR;

/// <summary>
/// Defines a plugin for registering SignalR hubs with the application.
/// Implement this interface to provide hub routing information for automatic endpoint mapping.
/// </summary>
[DoNotAutoRegister]
[DoNotInject]
public interface IHubRegistrationPlugin
{
    /// <summary>
    /// Gets the path used to identify the hub.
    /// </summary>
    string HubPath { get; }

    /// <summary>
    /// Gets the <see cref="Type"/> of the hub being registered.
    /// </summary>
    Type HubType { get; }
}