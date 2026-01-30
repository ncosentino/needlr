using NexusLabs.Needlr.SignalR;

namespace ChatHubExample;

/// <summary>
/// Registers the ChatHub with Needlr's SignalR plugin system.
/// This class is discovered at compile-time by the source generator,
/// enabling AOT-safe hub registration without reflection.
/// </summary>
public sealed class ChatHubRegistration : IHubRegistrationPlugin
{
    public string HubPath => "/chathub";
    public Type HubType => typeof(ChatHub);
}
