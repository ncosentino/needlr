namespace NexusLabs.Needlr.AgentFramework;

/// <summary>
/// Configures per-agent resilience settings that override the global default
/// set by <c>UsingResilience()</c> on <see cref="AgentFrameworkSyringe"/>.
/// </summary>
/// <remarks>
/// Apply this attribute alongside <see cref="NeedlrAiAgentAttribute"/> to configure
/// agent-specific retry and timeout behaviour.
/// </remarks>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class AgentResilienceAttribute : Attribute
{
    /// <summary>Gets the maximum number of retry attempts (0 = no retry).</summary>
    public int MaxRetries { get; }

    /// <summary>Gets the per-attempt timeout in seconds (0 = no timeout).</summary>
    public int TimeoutSeconds { get; }

    /// <summary>
    /// Initialises a new <see cref="AgentResilienceAttribute"/>.
    /// </summary>
    /// <param name="maxRetries">Maximum number of retry attempts. Defaults to 2.</param>
    /// <param name="timeoutSeconds">Per-attempt timeout in seconds. 0 means no timeout.</param>
    public AgentResilienceAttribute(int maxRetries = 2, int timeoutSeconds = 0)
    {
        MaxRetries = maxRetries;
        TimeoutSeconds = timeoutSeconds;
    }
}
