namespace NexusLabs.Needlr.AgentFramework.Progress;

/// <summary>
/// Creates <see cref="IProgressReporter"/> instances scoped to a specific orchestration run.
/// </summary>
/// <remarks>
/// <para>
/// Two creation patterns are supported:
/// </para>
/// <list type="bullet">
/// <item>
/// <description>
/// <strong>Default sinks</strong> (<see cref="Create(string)"/>): Uses all
/// <see cref="IProgressSink"/> instances registered in DI — including those
/// auto-discovered by Needlr and any added manually via
/// <c>services.AddSingleton&lt;IProgressSink, T&gt;()</c>.
/// Best for simple applications with a single agentic workflow.
/// </description>
/// </item>
/// <item>
/// <description>
/// <strong>Per-orchestration sinks</strong> (<see cref="Create(string, IEnumerable{IProgressSink})"/>):
/// Uses <em>only</em> the sinks you provide — default DI sinks are ignored entirely.
/// Best for complex server applications (multi-tenant, parallel workflows) where
/// each orchestration requires its own reporting channel.
/// </description>
/// </item>
/// </list>
/// </remarks>
public interface IProgressReporterFactory
{
    /// <summary>
    /// Creates a reporter using the default sinks registered in DI.
    /// </summary>
    /// <param name="workflowId">Correlation ID for the orchestration run.</param>
    /// <returns>
    /// A reporter that delivers events to all DI-registered <see cref="IProgressSink"/>
    /// instances, or <see cref="NullProgressReporter.Instance"/> if no sinks are registered.
    /// </returns>
    /// <remarks>
    /// Default sinks include all <see cref="IProgressSink"/> implementations auto-discovered
    /// by Needlr plus any registered manually via
    /// <c>services.AddSingleton&lt;IProgressSink, T&gt;()</c>.
    /// To prevent a specific sink from being auto-discovered, decorate it with
    /// <see cref="DoNotAutoRegisterAttribute"/>.
    /// </remarks>
    IProgressReporter Create(string workflowId);

    /// <summary>
    /// Creates a reporter with specific sinks for this orchestration only.
    /// Default DI sinks are <strong>not</strong> included — only the sinks you
    /// pass here receive events.
    /// </summary>
    /// <param name="workflowId">Correlation ID for the orchestration run.</param>
    /// <param name="sinks">
    /// Sinks to receive events for this orchestration only. Pass an empty collection
    /// to get <see cref="NullProgressReporter.Instance"/> (zero-overhead no-op).
    /// </param>
    /// <returns>
    /// A reporter that delivers events exclusively to <paramref name="sinks"/>,
    /// or <see cref="NullProgressReporter.Instance"/> if the collection is empty.
    /// </returns>
    /// <remarks>
    /// Use this overload in multi-tenant or multi-workflow server applications where
    /// each orchestration requires its own isolated reporting channel (e.g., per-tenant
    /// SSE streams, per-workflow log files). The caller is responsible for managing
    /// the lifetime and disposal of the provided sinks.
    /// </remarks>
    IProgressReporter Create(string workflowId, IEnumerable<IProgressSink> sinks);
}
