using Moq;

using NexusLabs.Needlr.AgentFramework.Diagnostics;
using NexusLabs.Needlr.AgentFramework.Workflows.Sequential;
using NexusLabs.Needlr.AgentFramework.Workspace;

namespace NexusLabs.Needlr.AgentFramework.Tests.Sequential;

/// <summary>
/// Tests for <see cref="CritiqueAndReviseExecutor"/>. Because
/// <see cref="Microsoft.Agents.AI.AIAgent"/> is sealed and requires real
/// infrastructure, these tests use a
/// <see cref="TestableCritiqueAndReviseExecutor"/> that reproduces the
/// same control flow without AI dependencies.
/// </summary>
public class CritiqueAndReviseExecutorTests
{
    [Fact]
    public async Task ExecuteAsync_CriticPassesFirst_ReturnsSuccess()
    {
        var executor = new TestableCritiqueAndReviseExecutor(
            criticResponses: ["PASS: looks good"],
            passCheck: (_, feedback) => feedback?.Contains("PASS") == true,
            maxRetries: 3);
        var context = CreateContext("Review");

        var result = await executor.ExecuteAsync(context, CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal("PASS: looks good", result.ResponseText);
        Assert.Equal(0, executor.ReviserCallCount);
    }

    [Fact]
    public async Task ExecuteAsync_CriticFailsThenPasses_RevisesOnce()
    {
        var executor = new TestableCritiqueAndReviseExecutor(
            criticResponses: ["FAIL: needs work", "PASS: much better"],
            passCheck: (_, feedback) => feedback?.Contains("PASS") == true,
            maxRetries: 3);
        var context = CreateContext("Review");

        var result = await executor.ExecuteAsync(context, CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal("PASS: much better", result.ResponseText);
        Assert.Equal(1, executor.ReviserCallCount);
    }

    [Fact]
    public async Task ExecuteAsync_AllAttemptsFail_ReturnsFailed()
    {
        var executor = new TestableCritiqueAndReviseExecutor(
            criticResponses: ["FAIL: bad", "FAIL: still bad", "FAIL: no good"],
            passCheck: (_, feedback) => feedback?.Contains("PASS") == true,
            maxRetries: 2);
        var context = CreateContext("Review");

        var result = await executor.ExecuteAsync(context, CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.NotNull(result.Exception);
        Assert.Contains("did not pass after 3 attempts", result.Exception!.Message);
        Assert.Equal(2, executor.ReviserCallCount);
    }

    [Fact]
    public async Task ExecuteAsync_MaxRetriesZero_NoRevision()
    {
        var executor = new TestableCritiqueAndReviseExecutor(
            criticResponses: ["FAIL: no good"],
            passCheck: (_, feedback) => feedback?.Contains("PASS") == true,
            maxRetries: 0);
        var context = CreateContext("Review");

        var result = await executor.ExecuteAsync(context, CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal(0, executor.ReviserCallCount);
    }

    // -------------------------------------------------------------------------
    // Part C: PostPassCheck + OnRevisionCompleted hook tests
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_PostPassCheck_OverridesPassToFail()
    {
        // Critic says "APPROVED" → passCheck returns true
        // But postPassCheck returns false (e.g. found UNVERIFIED tags)
        // Loop should continue to revision
        var executor = new TestableCritiqueAndReviseExecutor(
            criticResponses: ["APPROVED: looks good", "APPROVED: better now"],
            passCheck: (_, feedback) => feedback?.Contains("APPROVED") == true,
            maxRetries: 1,
            postPassCheck: (ctx, feedback) => false);
        var context = CreateContext("Review");

        var result = await executor.ExecuteAsync(context, CancellationToken.None);

        // postPassCheck always returns false → despite passCheck passing,
        // it should revise and ultimately fail (maxRetries=1 means 2 total attempts)
        Assert.False(result.Succeeded);
        Assert.Equal(1, executor.ReviserCallCount);
    }

    [Fact]
    public async Task ExecuteAsync_PostPassCheck_Null_NoOverride()
    {
        // When postPassCheck is null, pass is honored as-is
        var executor = new TestableCritiqueAndReviseExecutor(
            criticResponses: ["PASS: looks good"],
            passCheck: (_, feedback) => feedback?.Contains("PASS") == true,
            maxRetries: 3,
            postPassCheck: null);
        var context = CreateContext("Review");

        var result = await executor.ExecuteAsync(context, CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal(0, executor.ReviserCallCount);
    }

    [Fact]
    public async Task ExecuteAsync_OnRevisionCompleted_CalledWithReviserOutput()
    {
        string? capturedRevision = null;
        var executor = new TestableCritiqueAndReviseExecutor(
            criticResponses: ["FAIL: needs work", "PASS: all good"],
            passCheck: (_, feedback) => feedback?.Contains("PASS") == true,
            maxRetries: 2,
            onRevisionCompleted: (ctx, reviserText) =>
            {
                capturedRevision = reviserText;
                return Task.CompletedTask;
            });
        var context = CreateContext("Review");

        var result = await executor.ExecuteAsync(context, CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.NotNull(capturedRevision);
        Assert.Contains("Revised", capturedRevision);
    }

    [Fact]
    public async Task ExecuteAsync_OnRevisionCompleted_Null_NoError()
    {
        // When onRevisionCompleted is null, no error
        var executor = new TestableCritiqueAndReviseExecutor(
            criticResponses: ["FAIL: needs work", "PASS: all good"],
            passCheck: (_, feedback) => feedback?.Contains("PASS") == true,
            maxRetries: 2,
            onRevisionCompleted: null);
        var context = CreateContext("Review");

        var result = await executor.ExecuteAsync(context, CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal(1, executor.ReviserCallCount);
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static StageExecutionContext CreateContext(string stageName)
    {
        var diagAccessor = new Mock<IAgentDiagnosticsAccessor>();
        diagAccessor.Setup(x => x.BeginCapture()).Returns(Mock.Of<IDisposable>());

        return new StageExecutionContext(
            new InMemoryWorkspace(),
            diagAccessor.Object,
            ProgressReporter: null,
            StageIndex: 0,
            TotalStages: 1,
            StageName: stageName);
    }

    /// <summary>
    /// Reproduces the critique-and-revise control flow without requiring real
    /// <see cref="Microsoft.Agents.AI.AIAgent"/> instances. Provides
    /// deterministic critic responses and tracks reviser invocations.
    /// </summary>
    private sealed class TestableCritiqueAndReviseExecutor : IStageExecutor
    {
        private readonly IReadOnlyList<string> _criticResponses;
        private readonly Func<IAgentRunDiagnostics?, string?, bool> _passCheck;
        private readonly int _maxRetries;
        private readonly Func<StageExecutionContext, string?, bool>? _postPassCheck;
        private readonly Func<StageExecutionContext, string, Task>? _onRevisionCompleted;

        public int ReviserCallCount { get; private set; }

        public TestableCritiqueAndReviseExecutor(
            IReadOnlyList<string> criticResponses,
            Func<IAgentRunDiagnostics?, string?, bool> passCheck,
            int maxRetries,
            Func<StageExecutionContext, string?, bool>? postPassCheck = null,
            Func<StageExecutionContext, string, Task>? onRevisionCompleted = null)
        {
            _criticResponses = criticResponses;
            _passCheck = passCheck;
            _maxRetries = maxRetries;
            _postPassCheck = postPassCheck;
            _onRevisionCompleted = onRevisionCompleted;
        }

        public async Task<StageExecutionResult> ExecuteAsync(
            StageExecutionContext context,
            CancellationToken cancellationToken)
        {
            var allDiagnostics = new List<IAgentRunDiagnostics>();
            string? feedback = null;
            bool passed = false;
            int criticIndex = 0;

            for (int i = 0; i <= _maxRetries; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Simulate critic
                using (context.DiagnosticsAccessor.BeginCapture())
                {
                    feedback = criticIndex < _criticResponses.Count
                        ? _criticResponses[criticIndex++]
                        : "FAIL: exhausted responses";

                    var diag = context.DiagnosticsAccessor.LastRunDiagnostics;
                    if (diag is not null)
                    {
                        allDiagnostics.Add(diag);
                    }

                    passed = _passCheck(diag, feedback);
                }

                if (passed && _postPassCheck is not null)
                {
                    passed = _postPassCheck(context, feedback);
                }

                if (passed)
                {
                    break;
                }

                // Simulate reviser (if not last attempt)
                if (i < _maxRetries)
                {
                    using (context.DiagnosticsAccessor.BeginCapture())
                    {
                        ReviserCallCount++;
                        var reviserText = $"Revised based on: {feedback}";
                        var diag = context.DiagnosticsAccessor.LastRunDiagnostics;
                        if (diag is not null)
                        {
                            allDiagnostics.Add(diag);
                        }

                        if (_onRevisionCompleted is not null)
                        {
                            await _onRevisionCompleted(context, reviserText);
                        }
                    }
                }
            }

            var lastDiag = allDiagnostics.Count > 0 ? allDiagnostics[^1] : null;

            var result = passed
                ? StageExecutionResult.Success(context.StageName, lastDiag, feedback)
                : StageExecutionResult.Failed(
                    context.StageName,
                    new InvalidOperationException(
                        $"Critique-and-revise did not pass after {_maxRetries + 1} attempts. Last feedback: {feedback}"),
                    lastDiag);

            return result;
        }
    }
}
