namespace NexusLabs.Needlr.SignalR;

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