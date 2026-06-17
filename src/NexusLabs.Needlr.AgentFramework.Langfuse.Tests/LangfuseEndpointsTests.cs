using System.Text;

namespace NexusLabs.Needlr.AgentFramework.Langfuse.Tests;

public sealed class LangfuseEndpointsTests
{
    [Fact]
    public void Resolve_ExplicitEuRegion_BuildsCloudOtelAndScoresEndpoints()
    {
        var options = new LangfuseOptions { PublicKey = "pk-lf-1", SecretKey = "sk-lf-2", Region = LangfuseRegion.Eu };

        var endpoints = LangfuseEndpoints.Resolve(options);

        Assert.Equal("https://cloud.langfuse.com/api/public/otel/v1/traces", endpoints.TracesEndpoint.ToString());
        Assert.Equal("https://cloud.langfuse.com/api/public/otel/v1/metrics", endpoints.MetricsEndpoint.ToString());
        Assert.Equal("https://cloud.langfuse.com/api/public/scores", endpoints.ScoresEndpoint.ToString());
    }

    [Theory]
    [InlineData(LangfuseRegion.Us, "https://us.cloud.langfuse.com/api/public/otel/v1/traces")]
    [InlineData(LangfuseRegion.Jp, "https://jp.cloud.langfuse.com/api/public/otel/v1/traces")]
    [InlineData(LangfuseRegion.Hipaa, "https://hipaa.cloud.langfuse.com/api/public/otel/v1/traces")]
    public void Resolve_KnownRegions_MapToExpectedBaseUrls(LangfuseRegion region, string expectedTraces)
    {
        var options = new LangfuseOptions { PublicKey = "pk", SecretKey = "sk", Region = region };

        var endpoints = LangfuseEndpoints.Resolve(options);

        Assert.Equal(expectedTraces, endpoints.TracesEndpoint.ToString());
    }

    [Fact]
    public void Resolve_ExplicitHost_TakesPrecedenceAndToleratesMissingTrailingSlash()
    {
        var options = new LangfuseOptions
        {
            PublicKey = "pk",
            SecretKey = "sk",
            Region = LangfuseRegion.Us,
            Host = "http://localhost:3000",
        };

        var endpoints = LangfuseEndpoints.Resolve(options);

        Assert.Equal("http://localhost:3000/api/public/otel/v1/traces", endpoints.TracesEndpoint.ToString());
        Assert.Equal("http://localhost:3000/api/public/scores", endpoints.ScoresEndpoint.ToString());
    }

    [Fact]
    public void Resolve_BuildsBasicAuthHeaderFromBase64OfKeys()
    {
        var options = new LangfuseOptions { PublicKey = "pk-lf-1", SecretKey = "sk-lf-2", Region = LangfuseRegion.Eu };
        var expected = "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes("pk-lf-1:sk-lf-2"));

        var endpoints = LangfuseEndpoints.Resolve(options);

        Assert.Equal(expected, endpoints.AuthorizationHeaderValue);
        Assert.Contains($"Authorization={expected}", endpoints.Headers, StringComparison.Ordinal);
        Assert.Contains("x-langfuse-ingestion-version=4", endpoints.Headers, StringComparison.Ordinal);
    }

    [Fact]
    public void Resolve_MissingKeys_Throws()
    {
        var options = new LangfuseOptions { PublicKey = "pk" };

        Assert.Throws<InvalidOperationException>(() => LangfuseEndpoints.Resolve(options));
    }

    [Fact]
    public void Resolve_NonHttpHost_Throws()
    {
        var options = new LangfuseOptions { PublicKey = "pk", SecretKey = "sk", Host = "ftp://example.com" };

        Assert.Throws<InvalidOperationException>(() => LangfuseEndpoints.Resolve(options));
    }

    [Fact]
    public void Resolve_NoHostOrRegion_Throws()
    {
        var options = new LangfuseOptions { PublicKey = "pk", SecretKey = "sk" };

        Assert.Throws<InvalidOperationException>(() => LangfuseEndpoints.Resolve(options));
    }
}
