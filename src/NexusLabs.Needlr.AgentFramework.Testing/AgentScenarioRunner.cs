using NexusLabs.Needlr.AgentFramework.Context;
using NexusLabs.Needlr.AgentFramework.Diagnostics;
using NexusLabs.Needlr.AgentFramework.Workspace;

namespace NexusLabs.Needlr.AgentFramework.Testing;

/// <summary>
/// Executes <see cref="IAgentScenario"/> instances with proper workspace isolation,
/// execution context scoping, and diagnostics capture.
/// </summary>
/// <remarks>
/// <para>
/// The runner handles the full seed → execute → verify lifecycle:
/// </para>
/// <list type="number">
///   <item>Creates an <see cref="InMemoryWorkspace"/> and calls <see cref="IAgentScenario.SeedWorkspace"/>.</item>
///   <item>Establishes an <see cref="IAgentExecutionContext"/> with user ID and workspace in properties.</item>
///   <item>Opens diagnostics capture.</item>
///   <item>Invokes the agent with the scenario's system and user prompts.</item>
///   <item>Calls <see cref="IAgentScenario.Verify"/> with the post-execution workspace and diagnostics.</item>
/// </list>
/// <para>
/// <strong>Deterministic testing (no real LLM):</strong> The runner uses the <see cref="IAgentFactory"/>
/// from DI, which respects the <c>ChatClientFactory</c> configured on the syringe. To run scenarios
/// without real LLM calls, wire a mock <c>IChatClient</c> via the syringe:
/// </para>
/// <code>
/// var sp = new Syringe()
///     .UsingReflection()
///     .UsingAgentFramework(af => af
///         .Configure(opts => opts.ChatClientFactory = _ => mockChatClient.Object))
///     .BuildServiceProvider(config);
///
/// var runner = new AgentScenarioRunner(
///     sp.GetRequiredService&lt;IAgentFactory&gt;(),
///     sp.GetRequiredService&lt;IAgentExecutionContextAccessor&gt;(),
///     sp.GetRequiredService&lt;IAgentDiagnosticsAccessor&gt;());
/// </code>
/// </remarks>
public sealed class AgentScenarioRunner
{
    private readonly IAgentFactory _agentFactory;
    private readonly IAgentExecutionContextAccessor _contextAccessor;
    private readonly IAgentDiagnosticsAccessor _diagnosticsAccessor;

    /// <param name="agentFactory">Factory for creating agents.</param>
    /// <param name="contextAccessor">Execution context accessor for scoping.</param>
    /// <param name="diagnosticsAccessor">Diagnostics accessor for capture.</param>
    public AgentScenarioRunner(
        IAgentFactory agentFactory,
        IAgentExecutionContextAccessor contextAccessor,
        IAgentDiagnosticsAccessor diagnosticsAccessor)
    {
        _agentFactory = agentFactory ?? throw new ArgumentNullException(nameof(agentFactory));
        _contextAccessor = contextAccessor ?? throw new ArgumentNullException(nameof(contextAccessor));
        _diagnosticsAccessor = diagnosticsAccessor ?? throw new ArgumentNullException(nameof(diagnosticsAccessor));
    }

    /// <summary>
    /// Runs the scenario with full lifecycle management: seed workspace, establish context,
    /// capture diagnostics, execute agent, verify outcomes.
    /// </summary>
    /// <param name="scenario">The scenario to run.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The result of the scenario run, including diagnostics and verification outcome.</returns>
    public async Task<ScenarioRunResult> RunAsync(
        IAgentScenario scenario,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scenario);

        // 1. Create and seed workspace
        var workspace = new InMemoryWorkspace();
        scenario.SeedWorkspace(workspace);

        // 2. Create agent with scenario's prompts
        var agent = _agentFactory.CreateAgent(opts =>
        {
            opts.Name = $"Scenario-{scenario.Name}";
            opts.Instructions = scenario.SystemPrompt;
        });

        // 3. Establish execution context with workspace in properties
        var executionContext = new AgentExecutionContext(
            UserId: $"scenario-runner",
            OrchestrationId: $"scenario-{scenario.Name}-{Guid.NewGuid():N}",
            Properties: new Dictionary<string, object> { ["workspace"] = workspace });

        IAgentRunDiagnostics? diagnostics = null;
        string? responseText = null;
        Exception? executionError = null;

        // 4. Run with context + diagnostics scoping
        using (_contextAccessor.BeginScope(executionContext))
        using (_diagnosticsAccessor.BeginCapture())
        {
            try
            {
                var response = await agent.RunAsync(
                    scenario.UserPrompt,
                    cancellationToken: cancellationToken);

                responseText = response.ToString();
            }
            catch (Exception ex)
            {
                executionError = ex;
            }

            diagnostics = _diagnosticsAccessor.LastRunDiagnostics;
        }

        // 5. Verify
        Exception? verificationError = null;
        try
        {
            scenario.Verify(workspace, diagnostics);
        }
        catch (Exception ex)
        {
            verificationError = ex;
        }

        return new ScenarioRunResult(
            ScenarioName: scenario.Name,
            Workspace: workspace,
            Diagnostics: diagnostics,
            ResponseText: responseText,
            ExecutionError: executionError,
            VerificationError: verificationError,
            Succeeded: executionError is null && verificationError is null);
    }
}

/// <summary>
/// Result of running a single <see cref="IAgentScenario"/>.
/// </summary>
public sealed record ScenarioRunResult(
    string ScenarioName,
    IWorkspace Workspace,
    IAgentRunDiagnostics? Diagnostics,
    string? ResponseText,
    Exception? ExecutionError,
    Exception? VerificationError,
    bool Succeeded);
