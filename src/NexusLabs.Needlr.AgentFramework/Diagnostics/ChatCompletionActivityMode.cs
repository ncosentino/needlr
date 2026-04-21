namespace NexusLabs.Needlr.AgentFramework.Diagnostics;

/// <summary>
/// Controls how Needlr's diagnostics middleware creates
/// <see cref="System.Diagnostics.Activity"/> spans for chat completion calls.
/// </summary>
public enum ChatCompletionActivityMode
{
    /// <summary>
    /// Always create a Needlr <c>agent.chat</c> activity, regardless of whether
    /// a parent <c>gen_ai.*</c> activity already exists. This is the default and
    /// is appropriate when Needlr is the only OTel instrumentation layer.
    /// </summary>
    Always,

    /// <summary>
    /// When a parent <see cref="System.Diagnostics.Activity"/> with a
    /// <c>gen_ai.</c>-prefixed operation name exists (from MEAI's
    /// <c>UseOpenTelemetry()</c> or MAF's <c>WithOpenTelemetry()</c>), add
    /// Needlr-specific tags to that activity instead of creating a new one.
    /// When no such parent exists, create the activity as normal.
    /// </summary>
    /// <remarks>
    /// Use this mode when both Needlr diagnostics and upstream OTel middleware
    /// are active to avoid duplicate spans for the same chat completion call.
    /// Metrics (counters, histograms) and in-process diagnostics recording are
    /// unaffected — only the <see cref="System.Diagnostics.Activity"/> creation
    /// is suppressed.
    /// </remarks>
    EnrichParent,
}
