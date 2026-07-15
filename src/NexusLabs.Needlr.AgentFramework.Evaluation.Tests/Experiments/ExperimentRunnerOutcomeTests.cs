using NexusLabs.Needlr.AgentFramework.Evaluation.Experiments;

namespace NexusLabs.Needlr.AgentFramework.Evaluation.Tests.Experiments;

public sealed class ExperimentRunnerOutcomeTests
{
    private readonly CancellationToken _cancellationToken = TestContext.Current.CancellationToken;

    [Fact]
    public async Task RunAsync_MixedExecutionOutcomes_IsolatesFailuresAndRetainsEveryItem()
    {
        var cases = Enumerable.Range(0, 4)
            .Select(index => new ExperimentCase<int>
            {
                Id = $"case-{index}",
                Value = index,
            })
            .ToArray();
        var definition = new ExperimentDefinition<int, int>
        {
            Name = "mixed",
            CaseSource = new LocalExperimentCaseSource<int>("local", cases),
            Task = (context, _) => context.Case.Value switch
            {
                1 => throw new InvalidOperationException("execution failed"),
                2 => throw new OperationCanceledException("task canceled"),
                _ => ValueTask.FromResult(context.Case.Value * 10),
            },
        };
        var runner = new ExperimentRunner();

        var result = await runner.RunAsync(
            definition,
            new ExperimentRunOptions { RunId = "run-1", MaxConcurrency = 4 },
            _cancellationToken);

        Assert.Equal(4, result.Result.Items.Count);
        Assert.Equal(
            [
                ExperimentItemStatus.Succeeded,
                ExperimentItemStatus.ExecutionFailed,
                ExperimentItemStatus.Canceled,
                ExperimentItemStatus.Succeeded,
            ],
            result.Result.Items.Select(item => item.Status).ToArray());
        Assert.Equal([true, false, false, true], result.Result.Items.Select(item => item.HasOutput).ToArray());
        Assert.Equal(0, result.Result.Items[0].Output);
        Assert.Equal(30, result.Result.Items[3].Output);
        Assert.Equal(ExperimentFailureCode.ExecutionFailed, result.Result.Items[1].Failure!.Code);
        Assert.Equal(typeof(InvalidOperationException).FullName, result.Result.Items[1].Failure!.ExceptionType);
        Assert.Equal("execution failed", result.Result.Items[1].Failure!.Message);
        Assert.Equal(ExperimentFailureCode.TaskCanceled, result.Result.Items[2].Failure!.Code);
        Assert.All(result.Result.Items, item => Assert.Single(item.Attempts));
        Assert.Equal(
            [
                ExperimentAttemptStatus.Succeeded,
                ExperimentAttemptStatus.Failed,
                ExperimentAttemptStatus.Canceled,
                ExperimentAttemptStatus.Succeeded,
            ],
            result.Result.Items.Select(item => item.Attempts[0].Status).ToArray());
    }
}
