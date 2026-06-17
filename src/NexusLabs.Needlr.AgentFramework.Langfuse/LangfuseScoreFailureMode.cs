namespace NexusLabs.Needlr.AgentFramework.Langfuse;

/// <summary>
/// Controls how a Langfuse scenario reacts when uploading an evaluation score to the Langfuse
/// Scores API fails.
/// </summary>
/// <remarks>
/// A score upload is a dashboard write that happens <em>after</em> an eval has already produced its
/// verdict. Treating a transient Langfuse outage as an eval failure would turn a green eval red for
/// reasons unrelated to the system under test, so the default is <see cref="NonFatal"/>.
/// </remarks>
public enum LangfuseScoreFailureMode
{
    /// <summary>
    /// A failed score upload is recorded (the session's failure counter is incremented and the
    /// configured error callback is invoked) but does not throw. The eval continues. This is the
    /// default and the recommended mode for eval harnesses.
    /// </summary>
    NonFatal = 0,

    /// <summary>
    /// A failed score upload throws <see cref="LangfuseException"/>. Use this when a missing score
    /// should hard-fail the caller.
    /// </summary>
    Strict = 1,
}
