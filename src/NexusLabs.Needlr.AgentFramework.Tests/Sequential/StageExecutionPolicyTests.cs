using Moq;

using NexusLabs.Needlr.AgentFramework.Diagnostics;
using NexusLabs.Needlr.AgentFramework.Progress;
using NexusLabs.Needlr.AgentFramework.Workflows.Sequential;
using NexusLabs.Needlr.AgentFramework.Workspace;

namespace NexusLabs.Needlr.AgentFramework.Tests.Sequential;

public class StageExecutionPolicyTests
{
    // -------------------------------------------------------------------------
    // Test: Defaults
    // -------------------------------------------------------------------------

    [Fact]
    public void Defaults_MaxAttemptsIsOne()
    {
        var policy = new StageExecutionPolicy();

        Assert.Equal(1, policy.MaxAttempts);
    }

    [Fact]
    public void Defaults_ShouldSkipIsNull()
    {
        var policy = new StageExecutionPolicy();

        Assert.Null(policy.ShouldSkip);
    }

    [Fact]
    public void Defaults_PostValidationIsNull()
    {
        var policy = new StageExecutionPolicy();

        Assert.Null(policy.PostValidation);
    }

    [Fact]
    public void Defaults_TokenBudgetIsNull()
    {
        var policy = new StageExecutionPolicy();

        Assert.Null(policy.TokenBudget);
    }

    // -------------------------------------------------------------------------
    // Test: ShouldSkip evaluation
    // -------------------------------------------------------------------------

    [Fact]
    public void ShouldSkip_WhenSetToTrue_ReturnsTrue()
    {
        var policy = new StageExecutionPolicy { ShouldSkip = _ => true };
        var context = CreateContext("Test");

        Assert.True(policy.ShouldSkip!(context));
    }

    [Fact]
    public void ShouldSkip_WhenSetToFalse_ReturnsFalse()
    {
        var policy = new StageExecutionPolicy { ShouldSkip = _ => false };
        var context = CreateContext("Test");

        Assert.False(policy.ShouldSkip!(context));
    }

    [Fact]
    public void ShouldSkip_ReceivesCorrectContext()
    {
        StageExecutionContext? captured = null;
        var policy = new StageExecutionPolicy
        {
            ShouldSkip = ctx =>
            {
                captured = ctx;
                return false;
            },
        };
        var context = CreateContext("MyStage");

        policy.ShouldSkip!(context);

        Assert.NotNull(captured);
        Assert.Equal("MyStage", captured!.StageName);
    }

    // -------------------------------------------------------------------------
    // Test: PostValidation
    // -------------------------------------------------------------------------

    [Fact]
    public void PostValidation_ReturnsNull_IndicatesSuccess()
    {
        var policy = new StageExecutionPolicy
        {
            PostValidation = _ => null,
        };
        var result = StageExecutionResult.Success("A", null, "text");

        Assert.Null(policy.PostValidation!(result));
    }

    [Fact]
    public void PostValidation_ReturnsError_IndicatesFailure()
    {
        var policy = new StageExecutionPolicy
        {
            PostValidation = _ => "validation failed",
        };
        var result = StageExecutionResult.Success("A", null, "text");

        Assert.Equal("validation failed", policy.PostValidation!(result));
    }

    // -------------------------------------------------------------------------
    // Test: Init properties
    // -------------------------------------------------------------------------

    [Fact]
    public void Init_MaxAttempts_IsRespected()
    {
        var policy = new StageExecutionPolicy { MaxAttempts = 5 };

        Assert.Equal(5, policy.MaxAttempts);
    }

    [Fact]
    public void Init_TokenBudget_IsRespected()
    {
        var policy = new StageExecutionPolicy { TokenBudget = 10_000 };

        Assert.Equal(10_000, policy.TokenBudget);
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
}
