using NexusLabs.Needlr.AgentFramework.Diagnostics;

using Xunit;

namespace NexusLabs.Needlr.AgentFramework.Tests.Diagnostics;

/// <summary>
/// Tests for the <see cref="StageTermination"/> wiring on
/// <see cref="IAgentStageResult"/>. Asserts the default-impl returns
/// <see langword="null"/> so existing third-party implementations of the
/// interface continue to compile without modification, and that custom
/// implementations can populate it.
/// </summary>
public sealed class IAgentStageResultTerminationTests
{
    [Fact]
    public void DefaultImpl_TerminationIsNull()
    {
        IAgentStageResult result = new MinimalImpl();
        Assert.Null(result.Termination);
    }

    [Fact]
    public void CustomImpl_CanPopulateTermination()
    {
        var termination = new StageTermination.MaxIterationsReached(Limit: 5, IterationsUsed: 5);
        IAgentStageResult result = new TerminationImpl(termination);
        Assert.Same(termination, result.Termination);
    }

    private sealed class MinimalImpl : IAgentStageResult
    {
        public string AgentName => "Test";
        public Microsoft.Extensions.AI.ChatResponse? FinalResponse => null;
        public IAgentRunDiagnostics? Diagnostics => null;
    }

    private sealed class TerminationImpl(StageTermination termination) : IAgentStageResult
    {
        public string AgentName => "Test";
        public Microsoft.Extensions.AI.ChatResponse? FinalResponse => null;
        public IAgentRunDiagnostics? Diagnostics => null;
        public IStageTermination? Termination => termination;
    }
}
