using System.Net;
using System.Text.Json;

namespace NexusLabs.Needlr.AgentFramework.Langfuse.Tests;

public sealed class LangfuseMetricsClientTests
{
    private static readonly Uri BaseUrl = new("https://lf.example/");

    [Fact]
    public async Task QueryAsync_SerializesQueryParamAndParsesRows()
    {
        var captured = new List<CapturedRequest>();
        using var http = LangfuseHttpStub.Create(
            _ => LangfuseHttpStub.Json(HttpStatusCode.OK, "{\"data\":[{\"name\":\"correctness\",\"avg_value\":0.8,\"count_count\":3}]}"),
            captured);
        var client = new LangfuseMetricsClient(new LangfuseApiClient(http, BaseUrl, "Basic x"));

        var result = await client.QueryAsync(
            new LangfuseMetricsQuery
            {
                View = LangfuseMetricsView.ScoresNumeric,
                Metrics = [new LangfuseMetric("value", "avg")],
                Dimensions = ["name"],
                Filters = [new LangfuseMetricsFilter("name", "=", "correctness")],
                FromTimestamp = new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero),
                ToTimestamp = new DateTimeOffset(2026, 6, 8, 0, 0, 0, TimeSpan.Zero),
            },
            TestContext.Current.CancellationToken);

        var get = Assert.Single(captured);
        Assert.Equal(HttpMethod.Get, get.Method);
        Assert.Equal("/api/public/metrics", get.Uri.AbsolutePath);

        var raw = get.Uri.Query;
        var encoded = raw[(raw.IndexOf("query=", StringComparison.Ordinal) + "query=".Length)..];
        using var queryJson = JsonDocument.Parse(Uri.UnescapeDataString(encoded));
        var root = queryJson.RootElement;
        Assert.Equal("scores-numeric", root.GetProperty("view").GetString());
        Assert.Equal("value", root.GetProperty("metrics")[0].GetProperty("measure").GetString());
        Assert.Equal("avg", root.GetProperty("metrics")[0].GetProperty("aggregation").GetString());
        Assert.Equal("name", root.GetProperty("dimensions")[0].GetProperty("field").GetString());
        Assert.Equal("name", root.GetProperty("filters")[0].GetProperty("column").GetString());

        Assert.Equal("correctness", Assert.Single(result.Rows)["name"].GetString());
        Assert.Equal(0.8, result.GetScalar("avg", "value"));
    }

    [Fact]
    public async Task GetScoreAverageAsync_BuildsNumericQueryAndExtractsAverage()
    {
        var captured = new List<CapturedRequest>();
        using var http = LangfuseHttpStub.Create(
            _ => LangfuseHttpStub.Json(HttpStatusCode.OK, "{\"data\":[{\"avg_value\":0.75}]}"),
            captured);
        var client = new LangfuseMetricsClient(new LangfuseApiClient(http, BaseUrl, "Basic x"));

        var average = await client.GetScoreAverageAsync(
            "correctness",
            new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 6, 8, 0, 0, 0, TimeSpan.Zero),
            environment: "ci",
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(0.75, average);

        var raw = Assert.Single(captured).Uri.Query;
        var encoded = raw[(raw.IndexOf("query=", StringComparison.Ordinal) + "query=".Length)..];
        using var queryJson = JsonDocument.Parse(Uri.UnescapeDataString(encoded));
        var filters = queryJson.RootElement.GetProperty("filters");
        Assert.Equal("scores-numeric", queryJson.RootElement.GetProperty("view").GetString());
        Assert.Equal("correctness", filters[0].GetProperty("value").GetString());
        Assert.Equal("environment", filters[1].GetProperty("column").GetString());
        Assert.Equal("ci", filters[1].GetProperty("value").GetString());
    }

    [Fact]
    public async Task GetScoreAverageAsync_NoData_ReturnsNull()
    {
        var captured = new List<CapturedRequest>();
        using var http = LangfuseHttpStub.Create(
            _ => LangfuseHttpStub.Json(HttpStatusCode.OK, "{\"data\":[]}"),
            captured);
        var client = new LangfuseMetricsClient(new LangfuseApiClient(http, BaseUrl, "Basic x"));

        var average = await client.GetScoreAverageAsync(
            "missing",
            new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 6, 8, 0, 0, 0, TimeSpan.Zero),
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Null(average);
    }
}
