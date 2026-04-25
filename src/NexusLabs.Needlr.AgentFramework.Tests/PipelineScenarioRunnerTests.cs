using Moq;

using NexusLabs.Needlr.AgentFramework.Budget;
using NexusLabs.Needlr.AgentFramework.Diagnostics;
using NexusLabs.Needlr.AgentFramework.Progress;
using NexusLabs.Needlr.AgentFramework.Testing;
using NexusLabs.Needlr.AgentFramework.Workflows.Sequential;
using NexusLabs.Needlr.AgentFramework.Workspace;

namespace NexusLabs.Needlr.AgentFramework.Tests;

public class PipelineScenarioRunnerTests
{
    // -------------------------------------------------------------------------
    // Constructor validation
    // -------------------------------------------------------------------------

    [Fact]
    public void Constructor_NullPipelineRunner_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new PipelineScenarioRunner(null!));
    }

    // -------------------------------------------------------------------------
    // RunAsync — null arguments
    // -------------------------------------------------------------------------

    [Fact]
    public async Task RunAsync_NullScenario_ThrowsArgumentNull()
    {
        var runner = CreateRunner();

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            runner.RunAsync(null!, Mock.Of<IServiceProvider>(), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task RunAsync_NullServices_ThrowsArgumentNull()
    {
        var runner = CreateRunner();
        var scenario = new Mock<IPipelineScenario>();

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            runner.RunAsync(scenario.Object, null!, TestContext.Current.CancellationToken));
    }

    // -------------------------------------------------------------------------
    // RunAsync — seeds workspace before pipeline
    // -------------------------------------------------------------------------

    [Fact]
    public async Task RunAsync_SeedsWorkspaceBeforePipeline()
    {
        var runner = CreateRunner();
        var services = Mock.Of<IServiceProvider>();

        var scenario = new Mock<IPipelineScenario>();
        scenario.Setup(s => s.Name).Returns("seed-test");
        scenario.Setup(s => s.SeedWorkspace(It.IsAny<IWorkspace>()))
            .Callback<IWorkspace>(ws => ws.TryWriteFile("seed.txt", "hello"));
        scenario.Setup(s => s.BuildPipeline(services))
            .Returns(Array.Empty<PipelineStage>());
        scenario.Setup(s => s.Verify(It.IsAny<IWorkspace>(), It.IsAny<IPipelineRunResult>()));

        var result = await runner.RunAsync(
            scenario.Object,
            services,
            TestContext.Current.CancellationToken);

        Assert.True(result.Workspace.FileExists("seed.txt"));
        scenario.Verify(s => s.SeedWorkspace(It.IsAny<IWorkspace>()), Times.Once);
    }

    // -------------------------------------------------------------------------
    // RunAsync — passes stages to runner
    // -------------------------------------------------------------------------

    [Fact]
    public async Task RunAsync_PassesStagesToRunner()
    {
        var runner = CreateRunner();
        var services = Mock.Of<IServiceProvider>();

        var stages = new[]
        {
            DelegateStage("A"),
            DelegateStage("B"),
        };

        var scenario = new Mock<IPipelineScenario>();
        scenario.Setup(s => s.Name).Returns("stages-test");
        scenario.Setup(s => s.SeedWorkspace(It.IsAny<IWorkspace>()));
        scenario.Setup(s => s.BuildPipeline(services)).Returns(stages);
        scenario.Setup(s => s.Verify(It.IsAny<IWorkspace>(), It.IsAny<IPipelineRunResult>()));

        var result = await runner.RunAsync(
            scenario.Object,
            services,
            TestContext.Current.CancellationToken);

        // The pipeline result should reflect all stages were executed
        Assert.Equal(2, result.PipelineResult.Stages.Count);
        Assert.Equal("A", result.PipelineResult.Stages[0].AgentName);
        Assert.Equal("B", result.PipelineResult.Stages[1].AgentName);
    }

    // -------------------------------------------------------------------------
    // RunAsync — calls verify with result
    // -------------------------------------------------------------------------

    [Fact]
    public async Task RunAsync_CallsVerifyWithResult()
    {
        var runner = CreateRunner();
        var services = Mock.Of<IServiceProvider>();
        IPipelineRunResult? capturedResult = null;

        var scenario = new Mock<IPipelineScenario>();
        scenario.Setup(s => s.Name).Returns("verify-test");
        scenario.Setup(s => s.SeedWorkspace(It.IsAny<IWorkspace>()));
        scenario.Setup(s => s.BuildPipeline(services))
            .Returns(Array.Empty<PipelineStage>());
        scenario.Setup(s => s.Verify(It.IsAny<IWorkspace>(), It.IsAny<IPipelineRunResult>()))
            .Callback<IWorkspace, IPipelineRunResult>((_, r) => capturedResult = r);

        await runner.RunAsync(
            scenario.Object,
            services,
            TestContext.Current.CancellationToken);

        scenario.Verify(
            s => s.Verify(It.IsAny<IWorkspace>(), It.IsAny<IPipelineRunResult>()),
            Times.Once);
        Assert.NotNull(capturedResult);
    }

    // -------------------------------------------------------------------------
    // RunAsync — returns scenario result
    // -------------------------------------------------------------------------

    [Fact]
    public async Task RunAsync_ReturnsScenarioResult()
    {
        var runner = CreateRunner();
        var services = Mock.Of<IServiceProvider>();

        var scenario = new Mock<IPipelineScenario>();
        scenario.Setup(s => s.Name).Returns("result-test");
        scenario.Setup(s => s.SeedWorkspace(It.IsAny<IWorkspace>()));
        scenario.Setup(s => s.BuildPipeline(services))
            .Returns(Array.Empty<PipelineStage>());
        scenario.Setup(s => s.Verify(It.IsAny<IWorkspace>(), It.IsAny<IPipelineRunResult>()));

        var result = await runner.RunAsync(
            scenario.Object,
            services,
            TestContext.Current.CancellationToken);

        Assert.Equal("result-test", result.ScenarioName);
        Assert.NotNull(result.Workspace);
        Assert.NotNull(result.PipelineResult);
        Assert.True(result.PipelineResult.Succeeded);
    }

    // -------------------------------------------------------------------------
    // PipelineScenarioResult record
    // -------------------------------------------------------------------------

    [Fact]
    public void PipelineScenarioResult_PreservesAllFields()
    {
        var ws = new InMemoryWorkspace();
        var pipelineResult = Mock.Of<IPipelineRunResult>();

        var result = new PipelineScenarioResult("test", ws, pipelineResult);

        Assert.Equal("test", result.ScenarioName);
        Assert.Same(ws, result.Workspace);
        Assert.Same(pipelineResult, result.PipelineResult);
    }

    // -------------------------------------------------------------------------
    // IPipelineScenario — interface shape
    // -------------------------------------------------------------------------

    [Fact]
    public void IPipelineScenario_HasExpectedMembers()
    {
        var members = typeof(IPipelineScenario)
            .GetMembers(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

        Assert.Contains(members, m => m.Name == "Name");
        Assert.Contains(members, m => m.Name == "Description");
        Assert.Contains(members, m => m.Name == "SeedWorkspace");
        Assert.Contains(members, m => m.Name == "BuildPipeline");
        Assert.Contains(members, m => m.Name == "GetOptions");
        Assert.Contains(members, m => m.Name == "Verify");
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static PipelineScenarioRunner CreateRunner()
    {
        var pipelineRunner = new SequentialPipelineRunner(
            CreateDiagnosticsAccessor(),
            new TokenBudgetTracker(),
            CreateProgressFactory());

        return new PipelineScenarioRunner(pipelineRunner);
    }

    private static IAgentDiagnosticsAccessor CreateDiagnosticsAccessor()
    {
        var mock = new Mock<IAgentDiagnosticsAccessor>();
        mock.Setup(x => x.BeginCapture()).Returns(Mock.Of<IDisposable>());
        return mock.Object;
    }

    private static IProgressReporterFactory CreateProgressFactory()
    {
        var reporter = new Mock<IProgressReporter>();
        reporter.Setup(x => x.NextSequence()).Returns(0);
        reporter.Setup(x => x.WorkflowId).Returns("test-workflow");

        var factory = new Mock<IProgressReporterFactory>();
        factory.Setup(x => x.Create(It.IsAny<string>())).Returns(reporter.Object);
        return factory.Object;
    }

    private static PipelineStage DelegateStage(string name) =>
        new(name, new DelegateStageExecutor((_, _) => Task.CompletedTask));
}
