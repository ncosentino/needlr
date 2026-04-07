using NexusLabs.Needlr.AgentFramework.Diagnostics;
using NexusLabs.Needlr.AgentFramework.Workspace;

namespace NexusLabs.Needlr.AgentFramework.Testing;

/// <summary>
/// Defines a self-contained agent test scenario with seed → execute → verify lifecycle.
/// Scenarios are discovered automatically when registered via DI.
/// </summary>
/// <remarks>
/// <para>
/// Each scenario declares its own system prompt, user prompt, and workspace seed data.
/// The <see cref="AgentScenarioRunner"/> handles workspace creation, context scoping,
/// diagnostics capture, and invokes <see cref="Verify"/> after execution.
/// </para>
/// <para>
/// For multi-agent pipeline scenarios, consumers can implement custom runner logic
/// that coordinates multiple agents using the scenario's prompts and workspace.
/// </para>
/// </remarks>
public interface IAgentScenario
{
    /// <summary>Gets the scenario name (used for selection and reporting).</summary>
    string Name { get; }

    /// <summary>Gets a human-readable description of what the scenario tests.</summary>
    string Description { get; }

    /// <summary>Gets the system prompt (agent instructions) for this scenario.</summary>
    string SystemPrompt { get; }

    /// <summary>Gets the user message that starts the agent run.</summary>
    string UserPrompt { get; }

    /// <summary>
    /// Populates the workspace with files needed before the agent runs.
    /// Called by the runner before execution begins.
    /// </summary>
    /// <param name="workspace">The workspace to seed with test data.</param>
    void SeedWorkspace(IWorkspace workspace);

    /// <summary>
    /// Verifies the scenario's expected outcomes after the agent run completes.
    /// Throw an exception (e.g., <see cref="ScenarioVerificationException"/>) to indicate failure.
    /// </summary>
    /// <param name="workspace">The workspace after agent execution (contains any files the agent wrote).</param>
    /// <param name="diagnostics">
    /// Diagnostics from the run, or <see langword="null"/> if diagnostics were not captured.
    /// </param>
    void Verify(IWorkspace workspace, IAgentRunDiagnostics? diagnostics);
}

/// <summary>
/// Thrown when a scenario's <see cref="IAgentScenario.Verify"/> method detects a failure.
/// </summary>
public sealed class ScenarioVerificationException : Exception
{
    /// <summary>Gets the scenario that failed.</summary>
    public string ScenarioName { get; }

    /// <param name="scenarioName">Name of the scenario.</param>
    /// <param name="message">Description of the verification failure.</param>
    public ScenarioVerificationException(string scenarioName, string message)
        : base($"Scenario '{scenarioName}' failed: {message}")
    {
        ScenarioName = scenarioName;
    }
}
