using Microsoft.Extensions.AI;

namespace NexusLabs.Needlr.AgentFramework.Evaluation.Tests;

internal sealed class InMemoryEvaluationCaptureStore : IEvaluationCaptureStore
{
    private readonly Dictionary<string, ChatResponse> _entries = new();

    public int TryGetCount { get; private set; }
    public int SaveCount { get; private set; }

    public IReadOnlyDictionary<string, ChatResponse> Entries => _entries;

    public Task<ChatResponse?> TryGetAsync(string key, CancellationToken cancellationToken)
    {
        TryGetCount++;
        _entries.TryGetValue(key, out var response);
        return Task.FromResult<ChatResponse?>(response);
    }

    public Task SaveAsync(string key, ChatResponse response, CancellationToken cancellationToken)
    {
        SaveCount++;
        _entries[key] = response;
        return Task.CompletedTask;
    }
}
