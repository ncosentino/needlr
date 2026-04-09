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
/// in order but asynchronously — there may be a small delay between
/// emission and sink receipt.
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
    private readonly IReadOnlyList<IProgressSink> _sinks;
    private readonly IProgressSequence _sequence;
    private readonly Task _consumer;
    private readonly CancellationTokenSource _cts = new();
    private readonly string? _parentAgentId;

    /// <summary>
    /// Creates a channel-based reporter with the given sinks.
    /// Starts a background consumer immediately.
    /// </summary>
    public ChannelProgressReporter(
        string workflowId,
        IReadOnlyList<IProgressSink> sinks,
        IProgressSequence sequence,
        string? agentId = null,
        string? parentAgentId = null,
        int depth = 0,
        int capacity = 1000)
    {
        WorkflowId = workflowId;
        _sinks = sinks;
        _sequence = sequence;
        AgentId = agentId;
        _parentAgentId = parentAgentId;
        Depth = depth;

        _channel = Channel.CreateBounded<IProgressEvent>(new BoundedChannelOptions(capacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false,
        });

        _consumer = Task.Run(ConsumeAsync);
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
        new ChannelProgressReporter(
            WorkflowId,
            _sinks,
            _sequence,
            agentId: agentId,
            parentAgentId: AgentId,
            depth: Depth + 1);

    /// <summary>
    /// Completes the channel and waits for the background consumer to drain
    /// all remaining events to sinks.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        // Complete the channel writer so the consumer drains remaining items
        // then finishes naturally when ReadAllAsync ends.
        _channel.Writer.TryComplete();
        await _consumer;
        _cts.Dispose();
    }

    private async Task ConsumeAsync()
    {
        try
        {
            await foreach (var evt in _channel.Reader.ReadAllAsync(_cts.Token))
            {
                for (int i = 0; i < _sinks.Count; i++)
                {
                    try
                    {
                        await _sinks[i].OnEventAsync(evt, _cts.Token);
                    }
                    catch
                    {
                        // Swallow sink exceptions
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Shutdown
        }
    }
}
