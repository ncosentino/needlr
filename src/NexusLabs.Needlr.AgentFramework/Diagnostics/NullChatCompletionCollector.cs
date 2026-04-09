namespace NexusLabs.Needlr.AgentFramework.Diagnostics;

/// <summary>
/// No-op <see cref="IChatCompletionCollector"/> returned when diagnostics middleware is not wired.
/// </summary>
[DoNotAutoRegister]
internal sealed class NullChatCompletionCollector : IChatCompletionCollector
{
    internal static readonly NullChatCompletionCollector Instance = new();

    public IReadOnlyList<ChatCompletionDiagnostics> DrainCompletions() => [];
}
