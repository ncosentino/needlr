using Microsoft.Extensions.AI;
using Microsoft.Extensions.AI.Evaluation;
using Microsoft.Extensions.AI.Evaluation.Reporting;
using Microsoft.Extensions.AI.Evaluation.Reporting.Formats.Json;
using Microsoft.Extensions.AI.Evaluation.Reporting.Storage;

using Moq;

using NexusLabs.Needlr.AgentFramework.Evaluation.Experiments;

namespace NexusLabs.Needlr.AgentFramework.Evaluation.Reporting.Tests;

public sealed class MeaiReportingExperimentAdapterTests
{
    private readonly CancellationToken _cancellationToken =
        TestContext.Current.CancellationToken;
    private readonly MockRepository _mocks = new(MockBehavior.Strict);

    [Fact]
    public void Adapter_IsNotPublic()
    {
        Assert.False(
            typeof(MeaiReportingExperimentAdapter<,>).IsPublic,
            "The coordinated Reporting adapter must remain an internal implementation detail.");
    }

    [Fact]
    public void WithMeaiReporting_AddsCoordinatedScopeAndEvaluator()
    {
        using var storage = new TemporaryTestDirectory();
        var configuration = new ReportingConfiguration(
            [new CountingMetricEvaluator("score")],
            new DiskBasedResultStore(storage.Path),
            executionName: "run-1");
        var definition = new ExperimentDefinition<string, ReportingTestOutput>
        {
            Name = "definition",
            CaseSource = new LocalExperimentCaseSource<string>(
                "local",
                [new ExperimentCase<string> { Id = "case-1", Value = "question" }]),
            Task = (_, _) => ValueTask.FromResult(CreateOutput("answer")),
        };

        var configured = definition.WithMeaiReporting(
            configuration,
            context => new EvaluationInputs(
                context.Output.Messages,
                context.Output.Response),
            new MeaiReportingExperimentOptions<string, ReportingTestOutput>
            {
                ResponseReuseMode = MeaiReportingResponseReuseMode.Disabled,
            });

        Assert.Null(definition.ItemEvaluator);
        Assert.Empty(definition.ItemScopes);
        Assert.NotNull(configured.ItemEvaluator);
        var scope = Assert.Single(configured.ItemScopes);
        Assert.IsType<MeaiReportingExperimentAdapter<string, ReportingTestOutput>>(scope);
        Assert.Equal(
            MeaiReportingExperimentSchema.ProviderName,
            scope.Name);
    }

    [Fact]
    public void WithMeaiReporting_ExistingItemEvaluatorThrows()
    {
        using var storage = new TemporaryTestDirectory();
        var configuration = new ReportingConfiguration(
            [new CountingMetricEvaluator("score")],
            new DiskBasedResultStore(storage.Path),
            executionName: "run-1");
        var definition = new ExperimentDefinition<string, ReportingTestOutput>
        {
            Name = "definition",
            CaseSource = new LocalExperimentCaseSource<string>(
                "local",
                [new ExperimentCase<string> { Id = "case-1", Value = "question" }]),
            Task = (_, _) => ValueTask.FromResult(CreateOutput("answer")),
            ItemEvaluator = (_, _) => ValueTask.FromResult(new EvaluationResult()),
        };

        var exception = Assert.Throws<InvalidOperationException>(() =>
            definition.WithMeaiReporting(
                configuration,
                context => new EvaluationInputs(
                    context.Output.Messages,
                    context.Output.Response),
                new MeaiReportingExperimentOptions<string, ReportingTestOutput>
                {
                    ResponseReuseMode = MeaiReportingResponseReuseMode.Disabled,
                }));

        Assert.Contains("already has an item evaluator", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunAsync_ExecutionNameMismatchFailsBeforeExecution()
    {
        using var storage = new TemporaryTestDirectory();
        var configuration = new ReportingConfiguration(
            [new CountingMetricEvaluator("score")],
            new DiskBasedResultStore(storage.Path),
            executionName: "different-run");
        var adapter = CreateAdapter(
            configuration,
            MeaiReportingResponseReuseMode.Disabled);
        var executions = 0;
        var definition = CreateDefinition(
            adapter,
            trialCount: 1,
            (_, _) =>
            {
                executions++;
                return ValueTask.FromResult(CreateOutput("answer"));
            });

        var outcome = await new ExperimentRunner().RunAsync(
            definition,
            new ExperimentRunOptions
            {
                RunId = "run-1",
                MaxConcurrency = 1,
            },
            _cancellationToken);

        var item = Assert.Single(outcome.Result.Items);
        Assert.Equal(0, executions);
        Assert.Equal(ExperimentItemStatus.PrerequisiteFailed, item.Status);
        Assert.Equal(
            ExperimentFailureCode.ItemScopePrerequisiteFailed,
            item.Failure!.Code);
        var publication = Assert.Single(item.Publications);
        Assert.Equal(ExperimentPublicationOperationStatus.Failed, publication.Status);
        Assert.Contains("must match", publication.Failure!.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunAsync_PersistsCompositeEvaluationWithMappedIdentity()
    {
        using var storage = new TemporaryTestDirectory();
        var firstEvaluator = new CountingMetricEvaluator("first");
        var secondEvaluator = new CountingMetricEvaluator("second");
        var chatClient = CreateChatClient("answer");
        const string runId = "run-composite";
        var configuration = DiskBasedReportingConfiguration.Create(
            storage.Path,
            [firstEvaluator, secondEvaluator],
            new ChatConfiguration(chatClient.Object),
            enableResponseCaching: false,
            executionName: runId);
        var adapter = CreateAdapter(
            configuration,
            MeaiReportingResponseReuseMode.Disabled,
            isRequired: true);
        var definition = CreateDefinition(
            adapter,
            trialCount: 2,
            async (context, cancellationToken) =>
            {
                var reporting = context.Features
                    .GetRequired<MeaiReportingExperimentItem>();
                var messages = CreateMessages(context.Case.Value);
                var response = await reporting.ChatConfiguration!.ChatClient
                    .GetResponseAsync(
                        messages,
                        cancellationToken: cancellationToken);
                return new ReportingTestOutput(messages, response);
            });

        var outcome = await new ExperimentRunner().RunAsync(
            definition,
            new ExperimentRunOptions
            {
                RunId = runId,
                MaxConcurrency = 2,
            },
            _cancellationToken);

        Assert.Equal(2, outcome.Result.Items.Count);
        Assert.All(outcome.Result.Items, item =>
        {
            Assert.Equal(ExperimentItemStatus.Succeeded, item.Status);
            Assert.Equal(2, item.Metrics.Count);
            Assert.Equal(["first", "second"], item.Metrics.Select(metric => metric.Name));
            var publication = Assert.Single(item.Publications);
            Assert.Equal(
                ExperimentPublicationOperationStatus.Succeeded,
                publication.Status);
            Assert.Equal(3, publication.Correlations.Count);
            Assert.Equal(
                runId,
                GetCorrelation(
                    publication,
                    MeaiReportingExperimentSchema.ExecutionNameCorrelationName));
            Assert.Equal(
                "case-1",
                GetCorrelation(
                    publication,
                    MeaiReportingExperimentSchema.ScenarioNameCorrelationName));
            Assert.Equal(
                item.TrialIndex.ToString(),
                GetCorrelation(
                    publication,
                    MeaiReportingExperimentSchema.IterationNameCorrelationName));
        });
        Assert.Equal(2, firstEvaluator.CallCount);
        Assert.Equal(2, secondEvaluator.CallCount);
        chatClient.Verify(
            client => client.GetResponseAsync(
                It.Is<IEnumerable<ChatMessage>>(messages => messages.Any()),
                It.Is<ChatOptions?>(options => options == null),
                It.IsAny<CancellationToken>()),
            Times.Exactly(2));
        var stored = await ReadResultsAsync(configuration.ResultStore, runId);
        Assert.Equal(2, stored.Count);
        Assert.Equal(["1", "2"], stored.Select(result => result.IterationName));
        Assert.All(stored, result =>
        {
            Assert.Equal(runId, result.ExecutionName);
            Assert.Equal("case-1", result.ScenarioName);
            Assert.Equal(2, result.EvaluationResult.Metrics.Count);
        });
        _mocks.VerifyAll();
    }

    [Theory]
    [InlineData(MeaiReportingResponseReuseMode.CaseAndTrialReplay, 2)]
    [InlineData(MeaiReportingResponseReuseMode.FreshPerRun, 4)]
    [InlineData(MeaiReportingResponseReuseMode.Disabled, 4)]
    public async Task RunAsync_ResponseReuseModeControlsTrialAndRunCacheIsolation(
        MeaiReportingResponseReuseMode responseReuseMode,
        int expectedCalls)
    {
        using var storage = new TemporaryTestDirectory();
        var evaluator = new CountingMetricEvaluator("score");
        var chatClient = CreateChatClient("cached answer");

        await RunTwoTrialExperimentAsync(
            storage.Path,
            "run-a",
            responseReuseMode,
            chatClient.Object,
            evaluator);
        await RunTwoTrialExperimentAsync(
            storage.Path,
            "run-b",
            responseReuseMode,
            chatClient.Object,
            evaluator);

        chatClient.Verify(
            client => client.GetResponseAsync(
                It.Is<IEnumerable<ChatMessage>>(messages => messages.Any()),
                It.Is<ChatOptions?>(options => options == null),
                It.IsAny<CancellationToken>()),
            Times.Exactly(expectedCalls));
        var stored = await ReadResultsAsync(
            new DiskBasedResultStore(storage.Path),
            executionName: null);
        Assert.Equal(4, stored.Count);
        Assert.Equal(
            ["run-a", "run-a", "run-b", "run-b"],
            stored
                .OrderBy(result => result.ExecutionName, StringComparer.Ordinal)
                .ThenBy(result => result.IterationName, StringComparer.Ordinal)
                .Select(result => result.ExecutionName));
        _mocks.VerifyAll();
    }

    [Theory]
    [InlineData(MeaiReportingResponseReuseMode.CaseAndTrialReplay, 1)]
    [InlineData(MeaiReportingResponseReuseMode.FreshPerRun, 1)]
    [InlineData(MeaiReportingResponseReuseMode.Disabled, 2)]
    public async Task RunAsync_RetriesReuseOnlyConfiguredResponses(
        MeaiReportingResponseReuseMode responseReuseMode,
        int expectedCalls)
    {
        using var storage = new TemporaryTestDirectory();
        var evaluator = new CountingMetricEvaluator("score");
        var chatClient = CreateChatClient("retry answer");
        const string runId = "run-retry";
        var configuration = CreateDiskConfiguration(
            storage.Path,
            runId,
            responseReuseMode,
            chatClient.Object,
            [evaluator]);
        var adapter = CreateAdapter(configuration, responseReuseMode);
        var definition = CreateDefinition(
            adapter,
            trialCount: 1,
            async (context, cancellationToken) =>
            {
                var reporting = context.Features
                    .GetRequired<MeaiReportingExperimentItem>();
                var messages = CreateMessages(context.Case.Value);
                var response = await reporting.ChatConfiguration!.ChatClient
                    .GetResponseAsync(
                        messages,
                        cancellationToken: cancellationToken);
                if (context.AttemptNumber == 1)
                {
                    throw new InvalidOperationException("retry after response");
                }

                return new ReportingTestOutput(messages, response);
            });

        var outcome = await new ExperimentRunner().RunAsync(
            definition,
            new ExperimentRunOptions
            {
                RunId = runId,
                MaxConcurrency = 1,
                RetryPolicy = new ExperimentRetryPolicy(
                    maxAttempts: 2,
                    retryOn: ExperimentRetryableOutcome.ExecutionFailure,
                    delay: TimeSpan.Zero),
            },
            _cancellationToken);

        var item = Assert.Single(outcome.Result.Items);
        Assert.Equal(ExperimentItemStatus.Succeeded, item.Status);
        Assert.Equal(2, item.Attempts.Count);
        Assert.Equal(1, evaluator.CallCount);
        chatClient.Verify(
            client => client.GetResponseAsync(
                It.Is<IEnumerable<ChatMessage>>(messages => messages.Any()),
                It.Is<ChatOptions?>(options => options == null),
                It.IsAny<CancellationToken>()),
            Times.Exactly(expectedCalls));
        var stored = await ReadResultsAsync(configuration.ResultStore, runId);
        Assert.Single(stored);
        _mocks.VerifyAll();
    }

    [Fact]
    public async Task RunAsync_InputProjectionFailureDoesNotRetryExecutionOrPersist()
    {
        using var storage = new TemporaryTestDirectory();
        var evaluator = new CountingMetricEvaluator("score");
        const string runId = "run-evaluator-failure";
        var configuration = new ReportingConfiguration(
            [evaluator],
            new DiskBasedResultStore(storage.Path),
            executionName: runId);
        var adapter = configuration.CreateExperimentAdapter<string, ReportingTestOutput>(
            _ => throw new InvalidOperationException("projection failed"),
            new MeaiReportingExperimentOptions<string, ReportingTestOutput>
            {
                ResponseReuseMode = MeaiReportingResponseReuseMode.Disabled,
            });
        var executions = 0;
        var definition = CreateDefinition(
            adapter,
            trialCount: 1,
            (_, _) =>
            {
                executions++;
                return ValueTask.FromResult(CreateOutput("answer"));
            });

        var outcome = await new ExperimentRunner().RunAsync(
            definition,
            new ExperimentRunOptions
            {
                RunId = runId,
                MaxConcurrency = 1,
                RetryPolicy = new ExperimentRetryPolicy(
                    maxAttempts: 3,
                    retryOn: ExperimentRetryableOutcome.ExecutionFailure,
                    delay: TimeSpan.Zero),
            },
            _cancellationToken);

        var item = Assert.Single(outcome.Result.Items);
        Assert.Equal(1, executions);
        Assert.Equal(ExperimentItemStatus.EvaluationFailed, item.Status);
        Assert.Single(item.Attempts);
        Assert.Equal(0, evaluator.CallCount);
        Assert.Equal(
            ExperimentPublicationOperationStatus.NotAttempted,
            Assert.Single(item.Publications).Status);
        var stored = await ReadResultsAsync(configuration.ResultStore, runId);
        Assert.Empty(stored);
    }

    [Fact]
    public async Task RunAsync_ResultStoreFailureIsPublicationFailureWithoutExecutionRetry()
    {
        var evaluator = new CountingMetricEvaluator("score");
        var resultStore = _mocks.Create<IEvaluationResultStore>();
        resultStore
            .Setup(store => store.WriteResultsAsync(
                It.Is<IEnumerable<ScenarioRunResult>>(results =>
                    results.Count() == 1
                    && results.Single().ExecutionName == "run-store-failure"),
                It.Is<CancellationToken>(token => !token.CanBeCanceled)))
            .Returns(ValueTask.FromException(new IOException("store failed")));
        const string runId = "run-store-failure";
        var configuration = new ReportingConfiguration(
            [evaluator],
            resultStore.Object,
            executionName: runId);
        var adapter = CreateAdapter(
            configuration,
            MeaiReportingResponseReuseMode.Disabled,
            isRequired: true);
        var executions = 0;
        var definition = CreateDefinition(
            adapter,
            trialCount: 1,
            (_, _) =>
            {
                executions++;
                return ValueTask.FromResult(CreateOutput("answer"));
            });

        var outcome = await new ExperimentRunner().RunAsync(
            definition,
            new ExperimentRunOptions
            {
                RunId = runId,
                MaxConcurrency = 1,
                RetryPolicy = new ExperimentRetryPolicy(
                    maxAttempts: 3,
                    retryOn: ExperimentRetryableOutcome.ExecutionFailure,
                    delay: TimeSpan.Zero),
            },
            _cancellationToken);

        var item = Assert.Single(outcome.Result.Items);
        Assert.Equal(1, executions);
        Assert.Equal(1, evaluator.CallCount);
        Assert.Equal(ExperimentItemStatus.Succeeded, item.Status);
        Assert.Single(item.Attempts);
        var publication = Assert.Single(item.Publications);
        Assert.Equal(ExperimentPublicationOperationStatus.Failed, publication.Status);
        Assert.Equal(ExperimentFailureStage.Publication, publication.Failure!.Stage);
        Assert.Contains("store failed", publication.Failure.Message, StringComparison.Ordinal);
        Assert.Equal(ExperimentPublicationStatus.Failed, outcome.PublicationStatus);
        resultStore.Verify(
            store => store.WriteResultsAsync(
                It.Is<IEnumerable<ScenarioRunResult>>(results => results.Count() == 1),
                It.Is<CancellationToken>(token => !token.CanBeCanceled)),
            Times.Once);
        _mocks.VerifyAll();
    }

    [Fact]
    public async Task RunAsync_CallerCancellationAbortsWithoutPersistingIncompleteScenario()
    {
        using var storage = new TemporaryTestDirectory();
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(
            _cancellationToken);
        var evaluator = new CountingMetricEvaluator("score");
        const string runId = "run-canceled";
        var configuration = new ReportingConfiguration(
            [evaluator],
            new DiskBasedResultStore(storage.Path),
            executionName: runId);
        var adapter = CreateAdapter(
            configuration,
            MeaiReportingResponseReuseMode.Disabled);
        var started = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var neverCompletes = new TaskCompletionSource<ReportingTestOutput>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var definition = CreateDefinition(
            adapter,
            trialCount: 1,
            async (_, cancellationToken) =>
            {
                started.SetResult();
                return await neverCompletes.Task.WaitAsync(cancellationToken);
            });
        var runTask = new ExperimentRunner().RunAsync(
            definition,
            new ExperimentRunOptions
            {
                RunId = runId,
                MaxConcurrency = 1,
            },
            cancellation.Token);
        await started.Task.WaitAsync(_cancellationToken);

        cancellation.Cancel();
        var exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => runTask);

        Assert.Equal(cancellation.Token, exception.CancellationToken);
        Assert.Equal(0, evaluator.CallCount);
        var stored = await ReadResultsAsync(configuration.ResultStore, runId);
        Assert.Empty(stored);
    }

    [Fact]
    public async Task RunAsync_GeneratesOfficialJsonReportFromStoredResults()
    {
        using var storage = new TemporaryTestDirectory();
        var evaluator = new CountingMetricEvaluator("report-score");
        const string runId = "run-report";
        var configuration = new ReportingConfiguration(
            [evaluator],
            new DiskBasedResultStore(storage.Path),
            executionName: runId);
        var adapter = CreateAdapter(
            configuration,
            MeaiReportingResponseReuseMode.Disabled);
        var definition = CreateDefinition(
            adapter,
            trialCount: 1,
            (_, _) => ValueTask.FromResult(CreateOutput("reported answer")));

        await new ExperimentRunner().RunAsync(
            definition,
            new ExperimentRunOptions
            {
                RunId = runId,
                MaxConcurrency = 1,
            },
            _cancellationToken);
        var stored = await ReadResultsAsync(configuration.ResultStore, runId);
        var reportPath = Path.Combine(storage.Path, "report.json");

        await new JsonReportWriter(reportPath)
            .WriteReportAsync(stored, _cancellationToken);
        var report = await File.ReadAllTextAsync(reportPath, _cancellationToken);

        Assert.NotEmpty(report);
        Assert.Contains(runId, report, StringComparison.Ordinal);
        Assert.Contains("case-1", report, StringComparison.Ordinal);
        Assert.Contains("report-score", report, StringComparison.Ordinal);
    }

    private static MeaiReportingExperimentAdapter<string, ReportingTestOutput>
        CreateAdapter(
            ReportingConfiguration configuration,
            MeaiReportingResponseReuseMode responseReuseMode,
            bool isRequired = false) =>
        configuration.CreateExperimentAdapter<string, ReportingTestOutput>(
            context => new EvaluationInputs(
                context.Output.Messages,
                context.Output.Response),
            new MeaiReportingExperimentOptions<string, ReportingTestOutput>
            {
                ResponseReuseMode = responseReuseMode,
                IsRequired = isRequired,
            });

    private static ExperimentDefinition<string, ReportingTestOutput> CreateDefinition(
        MeaiReportingExperimentAdapter<string, ReportingTestOutput> adapter,
        int trialCount,
        ExperimentTask<string, ReportingTestOutput> task) =>
        new()
        {
            Name = "meai-reporting-tests",
            CaseSource = new LocalExperimentCaseSource<string>(
                "local",
                [
                    new ExperimentCase<string>
                    {
                        Id = "case-1",
                        Value = "question",
                        TrialCount = trialCount,
                        Tags = ["reporting"],
                    },
                ]),
            Task = task,
            ItemScopes = [adapter],
            ItemEvaluator = adapter.EvaluateAsync,
        };

    private static ReportingConfiguration CreateDiskConfiguration(
        string storagePath,
        string runId,
        MeaiReportingResponseReuseMode responseReuseMode,
        IChatClient chatClient,
        IEnumerable<IEvaluator> evaluators) =>
        DiskBasedReportingConfiguration.Create(
            storagePath,
            evaluators,
            new ChatConfiguration(chatClient),
            enableResponseCaching:
                responseReuseMode != MeaiReportingResponseReuseMode.Disabled,
            executionName: runId);

    private static IReadOnlyList<ChatMessage> CreateMessages(string prompt) =>
        [new ChatMessage(ChatRole.User, prompt)];

    private static ReportingTestOutput CreateOutput(string text)
    {
        var messages = CreateMessages("question");
        return new ReportingTestOutput(
            messages,
            new ChatResponse([new ChatMessage(ChatRole.Assistant, text)]));
    }

    private async Task RunTwoTrialExperimentAsync(
        string storagePath,
        string runId,
        MeaiReportingResponseReuseMode responseReuseMode,
        IChatClient chatClient,
        IEvaluator evaluator)
    {
        var configuration = CreateDiskConfiguration(
            storagePath,
            runId,
            responseReuseMode,
            chatClient,
            [evaluator]);
        var adapter = CreateAdapter(configuration, responseReuseMode);
        var definition = CreateDefinition(
            adapter,
            trialCount: 2,
            async (context, cancellationToken) =>
            {
                var reporting = context.Features
                    .GetRequired<MeaiReportingExperimentItem>();
                var messages = CreateMessages(context.Case.Value);
                var response = await reporting.ChatConfiguration!.ChatClient
                    .GetResponseAsync(
                        messages,
                        cancellationToken: cancellationToken);
                return new ReportingTestOutput(messages, response);
            });

        await new ExperimentRunner().RunAsync(
            definition,
            new ExperimentRunOptions
            {
                RunId = runId,
                MaxConcurrency = 2,
            },
            _cancellationToken);
    }

    private Mock<IChatClient> CreateChatClient(string responseText)
    {
        var chatClient = _mocks.Create<IChatClient>();
        var metadata = new ChatClientMetadata(
            providerName: "test-provider",
            providerUri: new Uri("https://example.test"),
            defaultModelId: "test-model");
        chatClient
            .Setup(client => client.GetService(
                It.Is<Type>(type => type == typeof(ChatClientMetadata)),
                It.Is<object?>(serviceKey => serviceKey == null)))
            .Returns(metadata);
        chatClient
            .Setup(client => client.GetResponseAsync(
                It.Is<IEnumerable<ChatMessage>>(messages => messages.Any()),
                It.Is<ChatOptions?>(options => options == null),
                It.IsAny<CancellationToken>()))
            .Returns((
                IEnumerable<ChatMessage> _,
                ChatOptions? _,
                CancellationToken cancellationToken) =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                return Task.FromResult(new ChatResponse(
                    [new ChatMessage(ChatRole.Assistant, responseText)])
                {
                    ModelId = "test-model",
                });
            });
        return chatClient;
    }

    private async Task<IReadOnlyList<ScenarioRunResult>> ReadResultsAsync(
        IEvaluationResultStore store,
        string? executionName)
    {
        var results = new List<ScenarioRunResult>();
        await foreach (var result in store.ReadResultsAsync(
            executionName,
            cancellationToken: _cancellationToken))
        {
            results.Add(result);
        }

        return results;
    }

    private static string GetCorrelation(
        ExperimentItemPublicationResult publication,
        string name) =>
        Assert.Single(
            publication.Correlations,
            correlation =>
                correlation.Namespace
                    == MeaiReportingExperimentSchema.CorrelationNamespace
                && correlation.Name == name).Value;
}
