using Microsoft.AspNetCore.SignalR;

namespace AotSourceGenApp;

/// <summary>
/// Demonstrates a SignalR hub mapped without runtime reflection in the Native AOT host.
/// </summary>
internal sealed class AotChatHub : Hub;
