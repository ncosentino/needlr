using System.Net;
using System.Text.Json;

namespace NexusLabs.Needlr.AgentFramework.Langfuse.Tests;

public sealed class LangfuseDatasetClientTests
{
    private static readonly Uri BaseUrl = new("https://lf.example/");
    private readonly CancellationToken _cancellationToken = TestContext.Current.CancellationToken;

    [Fact]
    public async Task EnsureDatasetAsync_UpsertsDataset()
    {
        var captured = new List<CapturedRequest>();
        using var httpClient = LangfuseHttpStub.Create(
            _ => LangfuseHttpStub.Json(HttpStatusCode.OK, "{\"name\":\"evals\"}"),
            captured);

        var client = new LangfuseDatasetClient(new LangfuseApiClient(httpClient, BaseUrl, "Basic x"));

        await client.EnsureDatasetAsync("evals", "regression suite", _cancellationToken);

        var post = Assert.Single(captured, c => c.Method == HttpMethod.Post);
        Assert.EndsWith("/api/public/v2/datasets", post.Uri.AbsolutePath, StringComparison.Ordinal);
        using var json = JsonDocument.Parse(post.Body!);
        Assert.Equal("evals", json.RootElement.GetProperty("name").GetString());
        Assert.Equal("regression suite", json.RootElement.GetProperty("description").GetString());
    }

    [Fact]
    public async Task EnsureDatasetAsync_WhenRepeated_UsesTheSameIdempotentUpsert()
    {
        var captured = new List<CapturedRequest>();
        using var httpClient = LangfuseHttpStub.Create(
            _ => LangfuseHttpStub.Json(HttpStatusCode.OK, "{\"name\":\"evals\"}"),
            captured);

        var client = new LangfuseDatasetClient(new LangfuseApiClient(httpClient, BaseUrl, "Basic x"));

        await client.EnsureDatasetAsync("evals");

        var post = Assert.Single(captured);
        Assert.Equal(HttpMethod.Post, post.Method);
        Assert.EndsWith("/api/public/v2/datasets", post.Uri.AbsolutePath, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UpsertItemAsync_PostsItemWithSerializedPayload()
    {
        var captured = new List<CapturedRequest>();
        using var httpClient = LangfuseHttpStub.Create(
            _ => new HttpResponseMessage(HttpStatusCode.OK),
            captured);

        var client = new LangfuseDatasetClient(new LangfuseApiClient(httpClient, BaseUrl, "Basic x"));

        await client.UpsertItemAsync(
            new LangfuseDatasetItem
            {
                DatasetName = "evals",
                Id = "case-1",
                Input = new { question = "2+2?" },
                ExpectedOutput = "4",
            },
            _cancellationToken);

        var post = Assert.Single(captured);
        Assert.Equal(HttpMethod.Post, post.Method);
        Assert.EndsWith("/api/public/dataset-items", post.Uri.AbsolutePath, StringComparison.Ordinal);

        using var json = JsonDocument.Parse(post.Body!);
        Assert.Equal("evals", json.RootElement.GetProperty("datasetName").GetString());
        Assert.Equal("case-1", json.RootElement.GetProperty("id").GetString());
        Assert.Equal("2+2?", json.RootElement.GetProperty("input").GetProperty("question").GetString());
        Assert.Equal("4", json.RootElement.GetProperty("expectedOutput").GetString());
    }

    [Fact]
    public async Task ListDatasetsAsync_MapsPageAndPreservesProviderOrder()
    {
        var captured = new List<CapturedRequest>();
        using var httpClient = LangfuseHttpStub.Create(
            _ => LangfuseDatasetApiStub.Page(
                page: 2,
                pageSize: 2,
                totalItems: 4,
                totalPages: 2,
                new
                {
                    id = "dataset-2",
                    projectId = "project-1",
                    name = "second",
                    description = "second dataset",
                    metadata = new { owner = "team-b" },
                    inputSchema = (object?)null,
                    expectedOutputSchema = (object?)null,
                    createdAt = "2026-07-02T00:00:00Z",
                    updatedAt = "2026-07-03T00:00:00Z",
                },
                new
                {
                    id = "dataset-1",
                    projectId = "project-1",
                    name = "first",
                    description = "first dataset",
                    metadata = new { owner = "team-a" },
                    inputSchema = (object?)null,
                    expectedOutputSchema = (object?)null,
                    createdAt = "2026-07-01T00:00:00Z",
                    updatedAt = "2026-07-02T00:00:00Z",
                }),
            captured);
        var client = CreateClient(httpClient);

        var page = await client.ListDatasetsAsync(
            page: 2,
            pageSize: 2,
            cancellationToken: _cancellationToken);

        Assert.Equal(2, page.Page);
        Assert.Equal(2, page.PageSize);
        Assert.Equal(4, page.TotalItems);
        Assert.Equal(2, page.TotalPages);
        Assert.Equal(2, page.Items.Count);
        Assert.Equal(["dataset-2", "dataset-1"], page.Items.Select(dataset => dataset.Id));
        Assert.Equal(["second", "first"], page.Items.Select(dataset => dataset.Name));
        Assert.Equal("team-b", page.Items[0].Metadata?.GetProperty("owner").GetString());

        var request = Assert.Single(captured);
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal("/api/public/v2/datasets", request.Uri.AbsolutePath);
        Assert.Equal("?page=2&limit=2", request.Uri.Query);
    }

    [Fact]
    public async Task GetDatasetAsync_MaterializesEveryActiveItemInProviderOrder()
    {
        var captured = new List<CapturedRequest>();
        using var httpClient = LangfuseHttpStub.Create(
            request =>
            {
                if (request.RequestUri!.AbsolutePath == "/api/public/v2/datasets/evals")
                {
                    return LangfuseDatasetApiStub.Dataset(
                        "dataset-1",
                        "evals",
                        new { owner = "needlr" });
                }

                var page = GetQueryValue(request.RequestUri, "page");
                return page == "1"
                    ? LangfuseDatasetApiStub.Page(
                        page: 1,
                        pageSize: 2,
                        totalItems: 3,
                        totalPages: 2,
                        LangfuseDatasetApiStub.Item(
                            "item-3",
                            "dataset-1",
                            "evals",
                            input: new { prompt = "third" },
                            expectedOutput: new { answer = "three" },
                            metadata: new { category = "number" },
                            sourceTraceId: "trace-3",
                            sourceObservationId: "observation-3"),
                        LangfuseDatasetApiStub.Item(
                            "item-2",
                            "dataset-1",
                            "evals",
                            status: "ARCHIVED",
                            input: new { prompt = "archived" }))
                    : LangfuseDatasetApiStub.Page(
                        page: 2,
                        pageSize: 2,
                        totalItems: 3,
                        totalPages: 2,
                        LangfuseDatasetApiStub.Item(
                            "item-1",
                            "dataset-1",
                            "evals",
                            input: new { prompt = "first" }));
            },
            captured);
        var client = CreateClient(httpClient, pageSize: 2);
        var selection = new LangfuseDatasetSelection { Name = "evals" };

        var dataset = await client.GetDatasetAsync(selection, _cancellationToken);

        Assert.Same(selection, dataset.Selection);
        Assert.Equal("dataset-1", dataset.Dataset.Id);
        Assert.Equal("evals", dataset.Dataset.Name);
        Assert.Equal("needlr", dataset.Dataset.Metadata?.GetProperty("owner").GetString());
        Assert.Equal(2, dataset.Items.Count);
        Assert.Equal(["item-3", "item-1"], dataset.Items.Select(item => item.Id));
        Assert.Equal("third", dataset.Items[0].Input?.GetProperty("prompt").GetString());
        Assert.Equal("three", dataset.Items[0].ExpectedOutput?.GetProperty("answer").GetString());
        Assert.Equal("number", dataset.Items[0].Metadata?.GetProperty("category").GetString());
        Assert.Equal("trace-3", dataset.Items[0].SourceTraceId);
        Assert.Equal("observation-3", dataset.Items[0].SourceObservationId);

        var itemRequests = captured
            .Where(request => request.Uri.AbsolutePath == "/api/public/dataset-items")
            .ToArray();
        Assert.Equal(2, itemRequests.Length);
        Assert.Equal("1", GetQueryValue(itemRequests[0].Uri, "page"));
        Assert.Equal("2", GetQueryValue(itemRequests[1].Uri, "page"));
        Assert.All(
            itemRequests,
            request => Assert.Null(GetQueryValue(request.Uri, "version")));
    }

    [Fact]
    public async Task GetDatasetAsync_ExplicitVersionIsAppliedToEveryItemPage()
    {
        var captured = new List<CapturedRequest>();
        using var httpClient = LangfuseHttpStub.Create(
            request =>
            {
                if (request.RequestUri!.AbsolutePath == "/api/public/v2/datasets/evals")
                {
                    return LangfuseDatasetApiStub.Dataset("dataset-1", "evals");
                }

                var page = int.Parse(GetQueryValue(request.RequestUri, "page")!);
                return LangfuseDatasetApiStub.Page(
                    page,
                    pageSize: 1,
                    totalItems: 2,
                    totalPages: 2,
                    LangfuseDatasetApiStub.Item(
                        $"item-{page}",
                        "dataset-1",
                        "evals"));
            },
            captured);
        var client = CreateClient(httpClient, pageSize: 1);
        var version = new DateTimeOffset(2026, 7, 5, 6, 7, 8, TimeSpan.FromHours(-4));
        var selection = new LangfuseDatasetSelection
        {
            Name = "evals",
            Version = version,
        };

        var dataset = await client.GetDatasetAsync(selection, _cancellationToken);

        Assert.Equal(version, dataset.Selection.Version);
        Assert.Equal(2, dataset.Items.Count);
        var expectedVersion = "2026-07-05T10:07:08.0000000Z";
        var itemRequests = captured
            .Where(request => request.Uri.AbsolutePath == "/api/public/dataset-items")
            .ToArray();
        Assert.Equal(2, itemRequests.Length);
        Assert.All(
            itemRequests,
            request => Assert.Equal(expectedVersion, GetQueryValue(request.Uri, "version")));
    }

    [Fact]
    public async Task ListDatasetItemsAsync_FiltersArchivedItemsWithoutChangingProviderPagination()
    {
        var captured = new List<CapturedRequest>();
        using var httpClient = LangfuseHttpStub.Create(
            _ => LangfuseDatasetApiStub.Page(
                page: 1,
                pageSize: 2,
                totalItems: 2,
                totalPages: 1,
                LangfuseDatasetApiStub.Item("active", "dataset-1", "evals"),
                LangfuseDatasetApiStub.Item(
                    "archived",
                    "dataset-1",
                    "evals",
                    status: "ARCHIVED")),
            captured);
        var client = CreateClient(httpClient);

        var page = await client.ListDatasetItemsAsync(
            new LangfuseDatasetSelection { Name = "evals" },
            page: 1,
            pageSize: 2,
            cancellationToken: _cancellationToken);

        var item = Assert.Single(page.Items);
        Assert.Equal("active", item.Id);
        Assert.Equal(2, page.TotalItems);
        Assert.Equal(1, page.TotalPages);
        var request = Assert.Single(captured);
        Assert.Equal("/api/public/dataset-items", request.Uri.AbsolutePath);
    }

    [Fact]
    public async Task GetDatasetAsync_DuplicateRemoteItemIdsAreRejected()
    {
        using var httpClient = LangfuseHttpStub.Create(
            request => request.RequestUri!.AbsolutePath == "/api/public/v2/datasets/evals"
                ? LangfuseDatasetApiStub.Dataset("dataset-1", "evals")
                : LangfuseDatasetApiStub.Page(
                    page: int.Parse(GetQueryValue(request.RequestUri, "page")!),
                    pageSize: 1,
                    totalItems: 2,
                    totalPages: 2,
                    LangfuseDatasetApiStub.Item("item-1", "dataset-1", "evals")),
            []);
        var client = CreateClient(httpClient, pageSize: 1);

        var exception = await Assert.ThrowsAsync<LangfuseException>(() =>
            client.GetDatasetAsync(
                new LangfuseDatasetSelection { Name = "evals" },
                _cancellationToken));

        Assert.Equal(
            "Langfuse dataset 'evals' returned duplicate dataset item id 'item-1'.",
            exception.Message);
    }

    [Fact]
    public async Task GetDatasetAsync_MalformedJsonIsReportedAsLangfuseFailure()
    {
        using var httpClient = LangfuseHttpStub.Create(
            _ => LangfuseHttpStub.Json(HttpStatusCode.OK, "{invalid-json"),
            []);
        var client = CreateClient(httpClient);

        var exception = await Assert.ThrowsAsync<LangfuseException>(() =>
            client.GetDatasetAsync(
                new LangfuseDatasetSelection { Name = "evals" },
                _cancellationToken));

        Assert.Equal(
            "Langfuse returned malformed JSON for response type 'LangfuseDatasetRef'.",
            exception.Message);
        Assert.IsType<JsonException>(exception.InnerException);
    }

    [Fact]
    public async Task GetDatasetAsync_MalformedPaginationIsRejected()
    {
        using var httpClient = LangfuseHttpStub.Create(
            request => request.RequestUri!.AbsolutePath == "/api/public/v2/datasets/evals"
                ? LangfuseDatasetApiStub.Dataset("dataset-1", "evals")
                : LangfuseDatasetApiStub.Page(
                    page: 2,
                    pageSize: 1,
                    totalItems: 1,
                    totalPages: 1,
                    LangfuseDatasetApiStub.Item("item-1", "dataset-1", "evals")),
            []);
        var client = CreateClient(httpClient, pageSize: 1);

        var exception = await Assert.ThrowsAsync<LangfuseException>(() =>
            client.GetDatasetAsync(
                new LangfuseDatasetSelection { Name = "evals" },
                _cancellationToken));

        Assert.Equal(
            "Langfuse dataset item response for 'evals' reported page 2 while page 1 was requested.",
            exception.Message);
    }

    [Fact]
    public async Task GetDatasetAsync_CancellationStopsRemoteLoading()
    {
        var itemRequestStarted = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseItemRequest = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        using var httpClient = new HttpClient(new DelegateHttpMessageHandler(
            async (request, cancellationToken) =>
            {
                if (request.RequestUri!.AbsolutePath == "/api/public/v2/datasets/evals")
                {
                    return LangfuseDatasetApiStub.Dataset("dataset-1", "evals");
                }

                itemRequestStarted.TrySetResult();
                await releaseItemRequest.Task.WaitAsync(cancellationToken);
                return LangfuseDatasetApiStub.Page(
                    page: 1,
                    pageSize: 1,
                    totalItems: 0,
                    totalPages: 0);
            }));
        var client = CreateClient(httpClient, pageSize: 1);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(_cancellationToken);

        var loadTask = client.GetDatasetAsync(
            new LangfuseDatasetSelection { Name = "evals" },
            cancellation.Token);
        await itemRequestStarted.Task.WaitAsync(_cancellationToken);
        cancellation.Cancel();

        var exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => loadTask);

        Assert.Equal(cancellation.Token, exception.CancellationToken);
    }

    private static LangfuseDatasetClient CreateClient(
        HttpClient httpClient,
        int pageSize = 100) =>
        new(
            new LangfuseApiClient(httpClient, BaseUrl, "Basic x"),
            pageSize);

    private static string? GetQueryValue(Uri uri, string name)
    {
        foreach (var component in uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var pair = component.Split('=', 2);
            if (string.Equals(pair[0], name, StringComparison.Ordinal))
            {
                return pair.Length == 1 ? string.Empty : Uri.UnescapeDataString(pair[1]);
            }
        }

        return null;
    }
}
