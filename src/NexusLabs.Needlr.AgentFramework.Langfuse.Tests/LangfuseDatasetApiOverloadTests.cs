using System.Net;
using System.Reflection;
using System.Text.Json;

namespace NexusLabs.Needlr.AgentFramework.Langfuse.Tests;

public sealed class LangfuseDatasetApiOverloadTests
{
    private static readonly Uri BaseUrl = new("https://lf.example/");
    private static readonly NullabilityInfoContext Nullability = new();
    private readonly CancellationToken _cancellationToken = TestContext.Current.CancellationToken;

    [Fact]
    public void PublicDatasetApis_ExposeOnlyExactExplicitNonOptionalOverloads()
    {
        AssertDatasetClientSurface(typeof(ILangfuseDatasetClient), expectAbstract: true);

        foreach (var implementationType in new[]
        {
            typeof(LangfuseDatasetClient),
            typeof(DisabledLangfuseDatasetClient),
        })
        {
            AssertDatasetClientSurface(implementationType, expectAbstract: false);
        }
    }

    [Fact]
    public async Task TokenlessListDatasetsOverload_UsesDefaultFirstPageOfFifty()
    {
        var captured = new List<CapturedRequest>();
        using var httpClient = LangfuseHttpStub.Create(
            _ => LangfuseDatasetApiStub.Page(page: 1, pageSize: 50, totalItems: 0, totalPages: 0),
            captured);
        var client = new LangfuseDatasetClient(new LangfuseApiClient(httpClient, BaseUrl, "Basic x"));

#pragma warning disable xUnit1051 // Intentionally exercises the tokenless dataset-list overload.
        var page = await client.ListDatasetsAsync();
#pragma warning restore xUnit1051

        Assert.Equal(1, page.Page);
        Assert.Equal(50, page.PageSize);
        var request = Assert.Single(captured);
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal("?page=1&limit=50", request.Uri.Query);
    }

    [Fact]
    public async Task TokenlessListDatasetItemsOverload_UsesDefaultFirstPageOfFifty()
    {
        var captured = new List<CapturedRequest>();
        using var httpClient = LangfuseHttpStub.Create(
            _ => LangfuseDatasetApiStub.Page(page: 1, pageSize: 50, totalItems: 0, totalPages: 0),
            captured);
        var client = new LangfuseDatasetClient(new LangfuseApiClient(httpClient, BaseUrl, "Basic x"));

#pragma warning disable xUnit1051 // Intentionally exercises the tokenless dataset-item-list overload.
        var page = await client.ListDatasetItemsAsync(new LangfuseDatasetSelection { Name = "evals" });
#pragma warning restore xUnit1051

        Assert.Equal(1, page.Page);
        Assert.Equal(50, page.PageSize);
        var request = Assert.Single(captured);
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal(
            "?datasetName=evals&page=1&limit=50",
            request.Uri.Query);
    }

    [Fact]
    public async Task TokenlessGetDatasetOverload_MaterializesDataset()
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
        var client = new LangfuseDatasetClient(new LangfuseApiClient(httpClient, BaseUrl, "Basic x"));

#pragma warning disable xUnit1051 // Intentionally exercises the tokenless get-dataset overload.
        var dataset = await client.GetDatasetAsync(new LangfuseDatasetSelection { Name = "evals" });
#pragma warning restore xUnit1051

        Assert.Equal("dataset-1", dataset.Dataset.Id);
        Assert.Equal("item-1", Assert.Single(dataset.Items).Id);
    }

    [Fact]
    public async Task TokenlessEnsureDatasetOverload_CreatesDatasetWithNullDescription()
    {
        var captured = new List<CapturedRequest>();
        using var httpClient = LangfuseHttpStub.Create(
            _ => LangfuseHttpStub.Json(HttpStatusCode.OK, "{\"name\":\"evals\"}"),
            captured);
        var client = new LangfuseDatasetClient(new LangfuseApiClient(httpClient, BaseUrl, "Basic x"));

#pragma warning disable xUnit1051 // Intentionally exercises the tokenless ensure-dataset overload.
        await client.EnsureDatasetAsync("evals");
#pragma warning restore xUnit1051

        var post = Assert.Single(captured, c => c.Method == HttpMethod.Post);
        using var json = JsonDocument.Parse(post.Body!);
        Assert.Equal("evals", json.RootElement.GetProperty("name").GetString());
        Assert.False(
            json.RootElement.TryGetProperty("description", out _),
            "Expected no 'description' property when null.");
    }

    [Fact]
    public async Task TokenlessUpsertItemOverload_PostsItem()
    {
        var captured = new List<CapturedRequest>();
        using var httpClient = LangfuseHttpStub.Create(
            _ => new HttpResponseMessage(HttpStatusCode.OK),
            captured);
        var client = new LangfuseDatasetClient(new LangfuseApiClient(httpClient, BaseUrl, "Basic x"));

#pragma warning disable xUnit1051 // Intentionally exercises the tokenless upsert-item overload.
        await client.UpsertItemAsync(new LangfuseDatasetItem
        {
            DatasetName = "evals",
            Id = "case-1",
        });
#pragma warning restore xUnit1051

        var post = Assert.Single(captured);
        Assert.Equal(HttpMethod.Post, post.Method);
        Assert.EndsWith("/api/public/dataset-items", post.Uri.AbsolutePath, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TokenlessDisabledReadOverloads_DoNotObserveCancellation()
    {
        var client = new DisabledLangfuseDatasetClient();
        var selection = new LangfuseDatasetSelection { Name = "evals" };

#pragma warning disable xUnit1051 // Intentionally exercises the tokenless disabled-client overloads.
        var listDatasets = await Assert.ThrowsAsync<InvalidOperationException>(
            () => client.ListDatasetsAsync());
        var listItems = await Assert.ThrowsAsync<InvalidOperationException>(
            () => client.ListDatasetItemsAsync(selection));
        var getDataset = await Assert.ThrowsAsync<InvalidOperationException>(
            () => client.GetDatasetAsync(selection));
        await client.EnsureDatasetAsync("evals");
        await client.UpsertItemAsync(new LangfuseDatasetItem { DatasetName = "evals", Id = "case-1" });
#pragma warning restore xUnit1051

        Assert.DoesNotContain("canceled", listDatasets.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("canceled", listItems.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("canceled", getDataset.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static MethodInfo[] GetDeclaredMethods(Type type, string name) =>
        type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
            .Where(method => method.Name == name)
            .OrderBy(method => method.GetParameters().Length)
            .ToArray();

    private static void AssertDatasetClientSurface(Type type, bool expectAbstract)
    {
        var listDatasetsMethods = GetDeclaredMethods(type, nameof(ILangfuseDatasetClient.ListDatasetsAsync));
        Assert.Equal(2, listDatasetsMethods.Length);
        AssertMethod(
            listDatasetsMethods[0],
            expectAbstract,
            typeof(Task<LangfusePage<LangfuseDataset>>));
        AssertMethod(
            listDatasetsMethods[1],
            expectAbstract,
            typeof(Task<LangfusePage<LangfuseDataset>>),
            typeof(int),
            typeof(int),
            typeof(CancellationToken));

        var listItemsMethods = GetDeclaredMethods(type, nameof(ILangfuseDatasetClient.ListDatasetItemsAsync));
        Assert.Equal(2, listItemsMethods.Length);
        AssertMethod(
            listItemsMethods[0],
            expectAbstract,
            typeof(Task<LangfusePage<LangfuseDatasetItemSnapshot>>),
            typeof(LangfuseDatasetSelection));
        AssertMethod(
            listItemsMethods[1],
            expectAbstract,
            typeof(Task<LangfusePage<LangfuseDatasetItemSnapshot>>),
            typeof(LangfuseDatasetSelection),
            typeof(int),
            typeof(int),
            typeof(CancellationToken));

        var getDatasetMethods = GetDeclaredMethods(type, nameof(ILangfuseDatasetClient.GetDatasetAsync));
        Assert.Equal(2, getDatasetMethods.Length);
        AssertMethod(
            getDatasetMethods[0],
            expectAbstract,
            typeof(Task<LangfuseDatasetSnapshot>),
            typeof(LangfuseDatasetSelection));
        AssertMethod(
            getDatasetMethods[1],
            expectAbstract,
            typeof(Task<LangfuseDatasetSnapshot>),
            typeof(LangfuseDatasetSelection),
            typeof(CancellationToken));

        var ensureDatasetMethods = GetDeclaredMethods(type, nameof(ILangfuseDatasetClient.EnsureDatasetAsync));
        Assert.Equal(2, ensureDatasetMethods.Length);
        AssertMethod(
            ensureDatasetMethods[0],
            expectAbstract,
            typeof(Task),
            typeof(string));
        AssertMethod(
            ensureDatasetMethods[1],
            expectAbstract,
            typeof(Task),
            typeof(string),
            typeof(string),
            typeof(CancellationToken));
        AssertNullable(ensureDatasetMethods[1].GetParameters()[1]);

        var upsertItemMethods = GetDeclaredMethods(type, nameof(ILangfuseDatasetClient.UpsertItemAsync));
        Assert.Equal(2, upsertItemMethods.Length);
        AssertMethod(
            upsertItemMethods[0],
            expectAbstract,
            typeof(Task),
            typeof(LangfuseDatasetItem));
        AssertMethod(
            upsertItemMethods[1],
            expectAbstract,
            typeof(Task),
            typeof(LangfuseDatasetItem),
            typeof(CancellationToken));

        AssertNoOptionalParameters(
            listDatasetsMethods
                .Concat(listItemsMethods)
                .Concat(getDatasetMethods)
                .Concat(ensureDatasetMethods)
                .Concat(upsertItemMethods));
    }

    private static void AssertMethod(
        MethodInfo method,
        bool expectAbstract,
        Type returnType,
        params Type[] parameterTypes)
    {
        Assert.Equal(expectAbstract, method.IsAbstract);
        Assert.False(method.IsGenericMethod);
        Assert.Equal(returnType, method.ReturnType);
        Assert.Equal(
            parameterTypes,
            method.GetParameters().Select(parameter => parameter.ParameterType).ToArray());
    }

    private static void AssertNullable(ParameterInfo parameter) =>
        Assert.Equal(NullabilityState.Nullable, Nullability.Create(parameter).ReadState);

    private static void AssertNoOptionalParameters(IEnumerable<MethodInfo> methods) =>
        Assert.DoesNotContain(
            methods.SelectMany(method => method.GetParameters()),
            parameter => parameter.IsOptional);
}
