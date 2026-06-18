namespace NexusLabs.Needlr.AgentFramework.Langfuse;

/// <summary>
/// Posts comments to the Langfuse public Comments API (<c>POST /api/public/comments</c>) via the
/// shared <see cref="LangfuseApiClient"/>. Comment failures are non-fatal — they are routed to the
/// diagnostics callback rather than thrown — because a comment is auxiliary context, never the
/// result of an eval.
/// </summary>
/// <remarks>
/// Langfuse requires an explicit project id on each comment. The API key maps to exactly one
/// project, so the id is resolved once from <c>GET /api/public/projects</c> and cached.
/// </remarks>
internal sealed class LangfuseCommentRecorder
{
    private const string TraceObjectType = "TRACE";

    private readonly LangfuseApiClient _apiClient;
    private readonly Action<string>? _diagnostics;
    private readonly SemaphoreSlim _projectIdLock = new(1, 1);
    private string? _projectId;

    public LangfuseCommentRecorder(LangfuseApiClient apiClient, Action<string>? diagnostics)
    {
        ArgumentNullException.ThrowIfNull(apiClient);

        _apiClient = apiClient;
        _diagnostics = diagnostics;
    }

    /// <summary>Attaches a comment to a trace.</summary>
    /// <param name="traceId">The trace to comment on.</param>
    /// <param name="content">The comment content (Langfuse limits this to 5000 characters).</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    public async Task AddTraceCommentAsync(string traceId, string content, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(traceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(content);

        try
        {
            var projectId = await ResolveProjectIdAsync(cancellationToken).ConfigureAwait(false);
            if (projectId is null)
            {
                _diagnostics?.Invoke(
                    "Langfuse comment skipped: could not resolve the project id for the configured API key.");
                return;
            }

            var request = new LangfuseCommentRequest
            {
                ProjectId = projectId,
                ObjectType = TraceObjectType,
                ObjectId = traceId,
                Content = content,
            };

            await _apiClient.PostAsync("api/public/comments", request, cancellationToken).ConfigureAwait(false);
        }
        catch (LangfuseException ex)
        {
            _diagnostics?.Invoke($"Langfuse comment on trace '{traceId}' failed: {ex.Message}");
        }
    }

    private async Task<string?> ResolveProjectIdAsync(CancellationToken cancellationToken)
    {
        if (_projectId is not null)
        {
            return _projectId;
        }

        await _projectIdLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_projectId is not null)
            {
                return _projectId;
            }

            var response = await _apiClient
                .GetAsync<LangfuseProjectsResponse>("api/public/projects", cancellationToken)
                .ConfigureAwait(false);

            _projectId = response?.Data is { Count: > 0 } data && !string.IsNullOrWhiteSpace(data[0].Id)
                ? data[0].Id
                : null;

            return _projectId;
        }
        finally
        {
            _projectIdLock.Release();
        }
    }
}
