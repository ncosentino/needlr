using NexusLabs.Needlr.AgentFramework.Evaluation.Experiments;

namespace NexusLabs.Needlr.AgentFramework.Evaluation.Tests.Experiments;

public sealed class ExperimentRunnerSinkTests
{
    private readonly CancellationToken _cancellationToken = TestContext.Current.CancellationToken;

    [Fact]
    public async Task RunAsync_SinksReceiveSameResultInRegistrationOrder()
    {
        var calls = new List<string>();
        ExperimentRunResult<int, int>? firstResult = null;
        ExperimentRunResult<int, int>? secondResult = null;
        var outcome = await new ExperimentRunner().RunAsync(
            CreateDefinition(
                sinks:
                [
                    CreateSink("first", isRequired: false, (result, _) =>
                    {
                        calls.Add("first");
                        firstResult = result;
                        return ValueTask.FromResult(SucceededSink("first", isRequired: false));
                    }),
                    CreateSink("second", isRequired: true, (result, _) =>
                    {
                        calls.Add("second");
                        secondResult = result;
                        return ValueTask.FromResult(SucceededSink("second", isRequired: true));
                    }),
                ]),
            CreateOptions(),
            _cancellationToken);

        Assert.Equal(["first", "second"], calls);
        Assert.Same(outcome.Result, firstResult);
        Assert.Same(outcome.Result, secondResult);
        Assert.Equal(ExperimentPublicationStatus.Succeeded, outcome.PublicationStatus);
        Assert.Equal(["first", "second"], outcome.SinkResults.Select(result => result.Name));
    }

    [Fact]
    public async Task RunAsync_NoPublicationOperations_ReturnsNotRequested()
    {
        var outcome = await new ExperimentRunner().RunAsync(
            CreateDefinition(),
            CreateOptions(),
            _cancellationToken);

        Assert.Equal(ExperimentPublicationStatus.NotRequested, outcome.PublicationStatus);
        Assert.Empty(outcome.Result.Items.Single().Publications);
        Assert.Empty(outcome.SinkResults);
    }

    [Fact]
    public async Task RunAsync_AllNotAttemptedPublications_ReturnsNotRequested()
    {
        var outcome = await new ExperimentRunner().RunAsync(
            CreateDefinition(
                sinks:
                [
                    CreateSink("disabled", isRequired: true, (_, _) =>
                        ValueTask.FromResult(NotAttemptedSink(
                            "disabled",
                            isRequired: true))),
                ]),
            CreateOptions(),
            _cancellationToken);

        Assert.Equal(ExperimentPublicationStatus.NotRequested, outcome.PublicationStatus);
        Assert.Equal(
            ExperimentPublicationOperationStatus.NotAttempted,
            Assert.Single(outcome.SinkResults).Status);
    }

    [Fact]
    public async Task RunAsync_OptionalFailureWithoutSuccess_ReturnsPartiallyFailed()
    {
        var outcome = await new ExperimentRunner().RunAsync(
            CreateDefinition(
                sinks:
                [
                    CreateSink("optional", isRequired: false, (_, _) =>
                        ValueTask.FromResult(FailedSink(
                            "optional",
                            isRequired: false,
                            "optional failed"))),
                ]),
            CreateOptions(),
            _cancellationToken);

        Assert.Equal(ExperimentPublicationStatus.PartiallyFailed, outcome.PublicationStatus);
        Assert.Equal(
            ExperimentPublicationOperationStatus.Failed,
            Assert.Single(outcome.SinkResults).Status);
        Assert.Equal(ExperimentItemStatus.Succeeded, Assert.Single(outcome.Result.Items).Status);
    }

    [Fact]
    public async Task RunAsync_RequiredFailure_DoesNotChangePassingQualityDecision()
    {
        var outcome = await new ExperimentRunner().RunAsync(
            CreateDefinition(
                policies:
                [
                    new CallbackExperimentPolicy<int, int>("passing", () => { }),
                ],
                sinks:
                [
                    CreateSink("required", isRequired: true, (_, _) =>
                        throw new InvalidOperationException("required failed")),
                ]),
            CreateOptions(),
            _cancellationToken);

        Assert.Equal(ExperimentRunDecision.Passed, outcome.Result.Decision);
        Assert.Equal(ExperimentPublicationStatus.Failed, outcome.PublicationStatus);
        Assert.Equal(
            ExperimentFailureCode.ResultSinkFailed,
            Assert.Single(outcome.SinkResults).Failure!.Code);
    }

    [Fact]
    public async Task RunAsync_SinkFailuresAndMalformedResults_DoNotSuppressLaterSinks()
    {
        var laterCalls = 0;
        var outcome = await new ExperimentRunner().RunAsync(
            CreateDefinition(
                sinks:
                [
                    CreateSink("throwing", isRequired: false, (_, _) =>
                        throw new InvalidOperationException("threw")),
                    CreateSink("malformed", isRequired: false, (_, _) =>
                        ValueTask.FromResult<ExperimentSinkResult>(null!)),
                    CreateSink("invalid-failure", isRequired: false, (_, _) =>
                        ValueTask.FromResult(new ExperimentSinkResult
                        {
                            Name = "invalid-failure",
                            IsRequired = false,
                            Status = ExperimentPublicationOperationStatus.Failed,
                            Failure = new ExperimentFailure
                            {
                                Code = ExperimentFailureCode.ResultSinkFailed,
                                Stage = ExperimentFailureStage.Publication,
                                ExceptionType = " ",
                                Message = "invalid",
                                IsRetryable = false,
                            },
                        })),
                    CreateSink("later", isRequired: false, (_, _) =>
                    {
                        Interlocked.Increment(ref laterCalls);
                        return ValueTask.FromResult(SucceededSink(
                            "later",
                            isRequired: false));
                    }),
                ]),
            CreateOptions(),
            _cancellationToken);

        Assert.Equal(1, laterCalls);
        Assert.Equal(4, outcome.SinkResults.Count);
        Assert.Equal(
            [
                ExperimentPublicationOperationStatus.Failed,
                ExperimentPublicationOperationStatus.Failed,
                ExperimentPublicationOperationStatus.Failed,
                ExperimentPublicationOperationStatus.Succeeded,
            ],
            outcome.SinkResults.Select(result => result.Status));
        Assert.Equal(ExperimentPublicationStatus.PartiallyFailed, outcome.PublicationStatus);
    }

    [Theory]
    [InlineData(false, ExperimentPublicationStatus.PartiallyFailed)]
    [InlineData(true, ExperimentPublicationStatus.Failed)]
    public async Task RunAsync_ItemScopeFailureContributesToAggregateStatus(
        bool isRequired,
        ExperimentPublicationStatus expectedStatus)
    {
        var provider = new CallbackExperimentItemScopeProvider<int, int>(
            "scope",
            isRequired,
            ExperimentItemScopeFailureMode.BestEffort,
            (_, _) =>
            {
                IExperimentItemScope<int, int> scope =
                    new CallbackExperimentItemScope<int, int>(
                        new Dictionary<Type, object>(),
                        () => null,
                        (_, _) => ValueTask.FromResult(
                            new ExperimentItemPublicationResult
                            {
                                Name = "scope",
                                IsRequired = isRequired,
                                Status = ExperimentPublicationOperationStatus.Failed,
                                Failure = PublicationFailure(
                                    ExperimentFailureCode.ItemScopeFailed,
                                    "scope failed"),
                            }),
                        _ => ValueTask.CompletedTask,
                        () => ValueTask.CompletedTask);
                return ValueTask.FromResult(scope);
            });
        var outcome = await new ExperimentRunner().RunAsync(
            CreateDefinition(itemScopes: [provider]),
            CreateOptions(),
            _cancellationToken);

        Assert.Equal(expectedStatus, outcome.PublicationStatus);
        Assert.Empty(outcome.SinkResults);
        Assert.Equal(
            ExperimentPublicationOperationStatus.Failed,
            Assert.Single(Assert.Single(outcome.Result.Items).Publications).Status);
    }

    [Fact]
    public async Task RunAsync_CallerCancellationBeforeSinks_SkipsEverySink()
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(
            _cancellationToken);
        var sinkCalls = 0;
        var runTask = new ExperimentRunner().RunAsync(
            CreateDefinition(
                policies:
                [
                    new CallbackExperimentPolicy<int, int>(
                        "cancel",
                        cancellation.Cancel),
                ],
                sinks:
                [
                    CreateSink("sink", isRequired: true, (_, _) =>
                    {
                        Interlocked.Increment(ref sinkCalls);
                        return ValueTask.FromResult(SucceededSink(
                            "sink",
                            isRequired: true));
                    }),
                ]),
            CreateOptions(),
            cancellation.Token);

        var exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => runTask);

        Assert.Equal(cancellation.Token, exception.CancellationToken);
        Assert.Equal(0, sinkCalls);
    }

    [Fact]
    public async Task RunAsync_CallerCancellationDuringSink_SkipsLaterSinks()
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(
            _cancellationToken);
        var firstStarted = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var laterCalls = 0;
        var runTask = new ExperimentRunner().RunAsync(
            CreateDefinition(
                sinks:
                [
                    CreateSink("blocking", isRequired: true, async (_, token) =>
                    {
                        firstStarted.SetResult();
                        await Task.Delay(Timeout.InfiniteTimeSpan, token);
                        return SucceededSink("blocking", isRequired: true);
                    }),
                    CreateSink("later", isRequired: true, (_, _) =>
                    {
                        Interlocked.Increment(ref laterCalls);
                        return ValueTask.FromResult(SucceededSink(
                            "later",
                            isRequired: true));
                    }),
                ]),
            CreateOptions(),
            cancellation.Token);
        await firstStarted.Task.WaitAsync(_cancellationToken);

        cancellation.Cancel();
        var exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => runTask);

        Assert.Equal(cancellation.Token, exception.CancellationToken);
        Assert.Equal(0, laterCalls);
    }

    [Fact]
    public async Task RunAsync_SinkThatCancelsThenThrows_PropagatesCallerCancellation()
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(
            _cancellationToken);
        var laterCalls = 0;
        var runTask = new ExperimentRunner().RunAsync(
            CreateDefinition(
                sinks:
                [
                    CreateSink("canceling", isRequired: false, (_, _) =>
                    {
                        cancellation.Cancel();
                        throw new InvalidOperationException("canceled then failed");
                    }),
                    CreateSink("later", isRequired: false, (_, _) =>
                    {
                        Interlocked.Increment(ref laterCalls);
                        return ValueTask.FromResult(SucceededSink(
                            "later",
                            isRequired: false));
                    }),
                ]),
            CreateOptions(),
            cancellation.Token);

        var exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => runTask);

        Assert.Equal(cancellation.Token, exception.CancellationToken);
        Assert.Equal(0, laterCalls);
    }

    private static ExperimentDefinition<int, int> CreateDefinition(
        IReadOnlyList<IExperimentRunPolicy<int, int>>? policies = null,
        IReadOnlyList<IExperimentItemScopeProvider<int, int>>? itemScopes = null,
        IReadOnlyList<IExperimentResultSink<int, int>>? sinks = null) =>
        new()
        {
            Name = "sinks",
            CaseSource = new LocalExperimentCaseSource<int>(
                "local",
                [new ExperimentCase<int> { Id = "case-1", Value = 1 }]),
            Task = (_, _) => ValueTask.FromResult(42),
            Policies = policies ?? [],
            ItemScopes = itemScopes ?? [],
            Sinks = sinks ?? [],
        };

    private static ExperimentRunOptions CreateOptions() =>
        new()
        {
            RunId = "run-1",
            MaxConcurrency = 1,
        };

    private static CallbackExperimentResultSink<int, int> CreateSink(
        string name,
        bool isRequired,
        Func<
            ExperimentRunResult<int, int>,
            CancellationToken,
            ValueTask<ExperimentSinkResult>> publishAsync) =>
        new(name, isRequired, publishAsync);

    private static ExperimentSinkResult SucceededSink(
        string name,
        bool isRequired) =>
        new()
        {
            Name = name,
            IsRequired = isRequired,
            Status = ExperimentPublicationOperationStatus.Succeeded,
        };

    private static ExperimentSinkResult NotAttemptedSink(
        string name,
        bool isRequired) =>
        new()
        {
            Name = name,
            IsRequired = isRequired,
            Status = ExperimentPublicationOperationStatus.NotAttempted,
        };

    private static ExperimentSinkResult FailedSink(
        string name,
        bool isRequired,
        string message) =>
        new()
        {
            Name = name,
            IsRequired = isRequired,
            Status = ExperimentPublicationOperationStatus.Failed,
            Failure = PublicationFailure(
                ExperimentFailureCode.ResultSinkFailed,
                message),
        };

    private static ExperimentFailure PublicationFailure(
        ExperimentFailureCode code,
        string message) =>
        new()
        {
            Code = code,
            Stage = ExperimentFailureStage.Publication,
            ExceptionType = typeof(InvalidOperationException).FullName!,
            Message = message,
            IsRetryable = false,
        };
}
