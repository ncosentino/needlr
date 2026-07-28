using NexusLabs.Needlr.SignalR;

namespace AotSourceGenApp;

/// <summary>
/// Supplies compile-time routing metadata for <see cref="AotChatHub"/>.
/// </summary>
internal sealed class AotChatHubRegistration : IHubRegistrationPlugin
{
    /// <inheritdoc />
    public string HubPath => "/aot-chat";

    /// <inheritdoc />
    public Type HubType => typeof(AotChatHub);
}
