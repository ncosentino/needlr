using System.Threading.Channels;

namespace NexusLabs.Needlr.AgentFramework.Progress;

/// <summary>
/// Non-blocking <see cref="IProgressReporter"/> that pushes events to a
/// <see cref="Channel{T}"/> and drains them to sinks on a background task.
/// Use this when sinks do I/O (database, network) and you don't want to
/// block the agent pipeline.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Report"/> writes to the channel and returns immediately.
/// A background consumer drains events to all sinks. Events are delivered
/// in order but asynchronously.
/// </para>
/// <para>
/// <see cref="CreateChild"/> returns a lightweight wrapper that shares
/// the parent's channel — no additional background tasks are created.
/// </para>
/// <para>
/// Call <see cref="DisposeAsync"/> to drain remaining events and stop
/// the background consumer.
/// </para>
/// </remarks>
[DoNotAutoRegister]
public sealed class ChannelProgressReporter : IProgressReporter, IAsyncDisposable
{
    private readonly Channel<IProgressEvent> _channel;
    private readonly IProgressSequence _sequence;
    private readonly IProgressReporterErrorHandler _errorHandler;
    private readonly Task _consumer;

    /// <summary>
    /// Creates a channel-based reporter with the given sinks.
    /// Starts a background consumer immediately.
    /// </summary>
    public ChannelProgressReporter(
        string workflowId,
        IReadOnlyList<IProgressSink> sinks,
        IProgressSequence sequence,
        IProgressReporterErrorHandler? errorHandler = null,
        string? agentId = null,
        string? parentAgentId = null,
        int depth = 0,
        int capacity = 1000)
    {
        WorkflowId = workflowId;
        _sequence = sequence;
        _errorHandler = errorHandler ?? new NullProgressReporterErrorHandler();
        AgentId = agentId;
        Depth = depth;

        _channel = Channel.CreateBounded<IProgressEvent>(new BoundedChannelOptions(capacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false,
        });

        _consumer = Task.Run(() => ConsumeAsync(sinks));
    }

    /// <inheritdoc />
    public string WorkflowId { get; }

    /// <inheritdoc />
    public string? AgentId { get; }

    /// <inheritdoc />
    public int Depth { get; }

    /// <inheritdoc />
    public long NextSequence() => _sequence.Next();

    /// <inheritdoc />
    public void Report(IProgressEvent progressEvent)
    {
        _channel.Writer.TryWrite(progressEvent);
    }

    /// <inheritdoc />
    public IProgressReporter CreateChild(string agentId) =>
        new ChannelChildReporter(this, agentId);

    /// <summary>
    /// Completes the channel and waits for the background consumer to drain
    /// all remaining events to sinks.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        _channel.Writer.TryComplete();
        await _consumer;
    }

    private async Task ConsumeAsync(IReadOnlyList<IProgressSink> sinks)
    {
        try
        {
            await foreach (var evt in _channel.Reader.ReadAllAsync())
            {
                for (int i = 0; i < sinks.Count; i++)
                {
                    try
                    {
                        await sinks[i].OnEventAsync(evt, CancellationToken.None);
                    }
                    catch (Exception ex)
                    {
                        _errorHandler.OnSinkException(sinks[i], evt, ex);
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Shutdown
        }
    }

    /// <summary>
    /// Lightweight child reporter that shares the parent's channel.
    /// No background task — writes go directly to the parent's channel.
    /// </summary>
    private sealed class ChannelChildReporter : IProgressReporter
    {
        private readonly ChannelProgressReporter _root;

        internal ChannelChildReporter(ChannelProgressReporter root, string agentId)
        {
            _root = root;
            WorkflowId = root.WorkflowId;
            AgentId = agentId;
            Depth = root.Depth + 1;
        }

        public string WorkflowId { get; }
        public string? AgentId { get; }
        public int Depth { get; }

        public long NextSequence() => _root.NextSequence();

        public void Report(IProgressEvent progressEvent) =>
            _root.Report(progressEvent);

        public IProgressReporter CreateChild(string agentId) =>
            new ChannelChildReporter(_root, agentId);
    }
}
