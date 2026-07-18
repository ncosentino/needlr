using NexusLabs.Needlr.AgentFramework.Evaluation.Experiments;

namespace NexusLabs.Needlr.AgentFramework.Langfuse.Tests;

public sealed class LangfuseDatasetCaseSourceTests
{
    private static readonly Uri BaseUrl = new("https://lf.example/");
    private readonly CancellationToken _cancellationToken = TestContext.Current.CancellationToken;

    [Fact]
    public async Task LoadAsync_TokenlessOverloadLoadsHostedDataset()
    {
        using var httpClient = LangfuseHttpStub.Create(
            request => request.RequestUri!.AbsolutePath == "/api/public/v2/datasets/evals"
                ? LangfuseDatasetApiStub.Dataset("dataset-1", "evals")
                : LangfuseDatasetApiStub.Page(
                    page: 1,
                    pageSize: 100,
                    totalItems: 1,
                    totalPages: 1,
                    LangfuseDatasetApiStub.Item("item-1", "dataset-1", "evals")),
            []);
        var source = new LangfuseDatasetCaseSource<string>(
            CreateClient(httpClient),
            new LangfuseDatasetSelection { Name = "evals" },
            item => new ExperimentCase<string>
            {
                Id = item.Id,
                Value = item.Id,
            });

#pragma warning disable xUnit1051 // This test intentionally exercises the tokenless hosted-source overload.
        var result = await source.LoadAsync();
#pragma warning restore xUnit1051

        Assert.Equal("dataset-1", result.Source.Id);
        Assert.Equal("item-1", Assert.Single(result.Cases).Value);
    }

    [Fact]
    public async Task LoadAsync_MapsHostedFieldsAndExplicitSourceIdentity()
    {
        var captured = new List<CapturedRequest>();
        using var httpClient = LangfuseHttpStub.Create(
            request => request.RequestUri!.AbsolutePath == "/api/public/v2/datasets/evals"
                ? LangfuseDatasetApiStub.Dataset("dataset-1", "evals")
                : LangfuseDatasetApiStub.Page(
                    page: 1,
                    pageSize: 100,
                    totalItems: 1,
                    totalPages: 1,
                    LangfuseDatasetApiStub.Item(
                        "item-1",
                        "dataset-1",
                        "evals",
                        input: new { prompt = "question" },
                        expectedOutput: new { answer = "response" },
                        metadata: new { category = "regression" },
                        sourceTraceId: "trace-1",
                        sourceObservationId: "observation-1")),
            captured);
        var client = CreateClient(httpClient);
        var selection = new LangfuseDatasetSelection
        {
            Name = "evals",
            Version = new DateTimeOffset(2026, 7, 5, 6, 7, 8, TimeSpan.FromHours(-4)),
        };
        var source = new LangfuseDatasetCaseSource<MappedLangfuseDatasetCase>(
            client,
            selection,
            item => new ExperimentCase<MappedLangfuseDatasetCase>
            {
                Id = item.Id,
                Value = new MappedLangfuseDatasetCase(
                    item.Input!.Value.GetProperty("prompt").GetString()!,
                    item.ExpectedOutput!.Value.GetProperty("answer").GetString()!,
                    item.Metadata!.Value.GetProperty("category").GetString()!,
                    item.SourceTraceId,
                    item.SourceObservationId),
                TrialCount = 3,
                Tags = ["hosted"],
            });

        var result = await source.LoadAsync(_cancellationToken);

        Assert.Same(selection, source.Selection);
        Assert.Equal("evals", result.Source.Name);
        Assert.Equal("dataset-1", result.Source.Id);
        Assert.Equal("2026-07-05T10:07:08.0000000Z", result.Source.Version);
        var @case = Assert.Single(result.Cases);
        Assert.Equal("item-1", @case.Id);
        Assert.Equal(3, @case.TrialCount);
        Assert.Equal(["hosted"], @case.Tags);
        Assert.Equal(
            new MappedLangfuseDatasetCase(
                "question",
                "response",
                "regression",
                "trace-1",
                "observation-1"),
            @case.Value);

        var itemRequest = Assert.Single(
            captured,
            request => request.Uri.AbsolutePath == "/api/public/dataset-items");
        Assert.Contains(
            "version=2026-07-05T10%3A07%3A08.0000000Z",
            itemRequest.Uri.Query,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task LoadAsync_LatestSelectionOmitsVersionIdentity()
    {
        var captured = new List<CapturedRequest>();
        using var httpClient = LangfuseHttpStub.Create(
            request => request.RequestUri!.AbsolutePath == "/api/public/v2/datasets/evals"
                ? LangfuseDatasetApiStub.Dataset("dataset-1", "evals")
                : LangfuseDatasetApiStub.Page(
                    page: 1,
                    pageSize: 100,
                    totalItems: 1,
                    totalPages: 1,
                    LangfuseDatasetApiStub.Item("item-1", "dataset-1", "evals")),
            captured);
        var source = new LangfuseDatasetCaseSource<string>(
            CreateClient(httpClient),
            new LangfuseDatasetSelection { Name = "evals" },
            item => new ExperimentCase<string>
            {
                Id = item.Id,
                Value = item.Id,
            });

        var result = await source.LoadAsync(_cancellationToken);

        Assert.Null(result.Source.Version);
        var itemRequest = Assert.Single(
            captured,
            request => request.Uri.AbsolutePath == "/api/public/dataset-items");
        Assert.DoesNotContain("version=", itemRequest.Uri.Query, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LoadAsync_DuplicateMappedCaseIdsAreRejected()
    {
        using var httpClient = CreateTwoItemClient();
        var source = new LangfuseDatasetCaseSource<string>(
            CreateClient(httpClient),
            new LangfuseDatasetSelection { Name = "evals" },
            _ => new ExperimentCase<string>
            {
                Id = "duplicate",
                Value = "value",
            });

        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            source.LoadAsync(_cancellationToken).AsTask());

        Assert.Equal(
            "Langfuse dataset 'evals' mapped multiple items to experiment case id 'duplicate'. (Parameter 'mapper')",
            exception.Message);
    }

    [Fact]
    public async Task LoadAsync_CaseIdMustPreserveHostedItemIdentity()
    {
        using var httpClient = LangfuseHttpStub.Create(
            request => request.RequestUri!.AbsolutePath == "/api/public/v2/datasets/evals"
                ? LangfuseDatasetApiStub.Dataset("dataset-1", "evals")
                : LangfuseDatasetApiStub.Page(
                    page: 1,
                    pageSize: 100,
                    totalItems: 1,
                    totalPages: 1,
                    LangfuseDatasetApiStub.Item("item-1", "dataset-1", "evals")),
            []);
        var source = new LangfuseDatasetCaseSource<string>(
            CreateClient(httpClient),
            new LangfuseDatasetSelection { Name = "evals" },
            _ => new ExperimentCase<string>
            {
                Id = "different-id",
                Value = "value",
            });

        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            source.LoadAsync(_cancellationToken).AsTask());

        Assert.Equal(
            "Langfuse dataset item 'item-1' must map to the same experiment case id so later trial scopes can bind the hosted item. (Parameter 'mapper')",
            exception.Message);
    }

    [Fact]
    public async Task LoadAsync_DisabledClientCannotProduceAnEmptyPassingSource()
    {
        var source = new LangfuseDatasetCaseSource<string>(
            new DisabledLangfuseDatasetClient(),
            new LangfuseDatasetSelection { Name = "evals" },
            item => new ExperimentCase<string>
            {
                Id = item.Id,
                Value = item.Id,
            });

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            source.LoadAsync(_cancellationToken).AsTask());

        Assert.Equal(
            "Langfuse dataset 'evals' cannot be loaded because Langfuse is not configured.",
            exception.Message);
    }

    [Fact]
    public async Task LoadAsync_EmptyHostedDatasetIsRejected()
    {
        using var httpClient = LangfuseHttpStub.Create(
            request => request.RequestUri!.AbsolutePath == "/api/public/v2/datasets/evals"
                ? LangfuseDatasetApiStub.Dataset("dataset-1", "evals")
                : LangfuseDatasetApiStub.Page(
                    page: 1,
                    pageSize: 100,
                    totalItems: 0,
                    totalPages: 0),
            []);
        var source = new LangfuseDatasetCaseSource<string>(
            CreateClient(httpClient),
            new LangfuseDatasetSelection { Name = "evals" },
            item => new ExperimentCase<string>
            {
                Id = item.Id,
                Value = item.Id,
            });

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            source.LoadAsync(_cancellationToken).AsTask());

        Assert.Equal(
            "Langfuse dataset 'evals' contains no active items.",
            exception.Message);
    }

    private static LangfuseDatasetClient CreateClient(HttpClient httpClient) =>
        new(new LangfuseApiClient(httpClient, BaseUrl, "Basic x"));

    private static HttpClient CreateTwoItemClient() =>
        LangfuseHttpStub.Create(
            request => request.RequestUri!.AbsolutePath == "/api/public/v2/datasets/evals"
                ? LangfuseDatasetApiStub.Dataset("dataset-1", "evals")
                : LangfuseDatasetApiStub.Page(
                    page: 1,
                    pageSize: 100,
                    totalItems: 2,
                    totalPages: 1,
                    LangfuseDatasetApiStub.Item("item-1", "dataset-1", "evals"),
                    LangfuseDatasetApiStub.Item("item-2", "dataset-1", "evals")),
            []);
}
