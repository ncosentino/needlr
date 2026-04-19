namespace NexusLabs.Needlr.Copilot.Tests;

public class RateLimitDetectionTests
{
    [Fact]
    public void ThrowIfRateLimited_RateLimitText_ThrowsWithRetryAfter()
    {
        var result = CopilotWebSearchFunction.ParseSearchResult(
            "Rate limit exceeded (20 requests per minute). Try again in 38 seconds.");

        var ex = Assert.Throws<CopilotRateLimitException>(
            () => CopilotWebSearchFunction.ThrowIfRateLimited(result));

        Assert.Equal(TimeSpan.FromSeconds(38), ex.RetryAfter);
        Assert.Contains("Rate limit exceeded", ex.Message);
    }

    [Fact]
    public void ThrowIfRateLimited_TooManyRequests_Throws()
    {
        var result = CopilotWebSearchFunction.ParseSearchResult(
            "Too many requests. Please slow down.");

        Assert.Throws<CopilotRateLimitException>(
            () => CopilotWebSearchFunction.ThrowIfRateLimited(result));
    }

    [Fact]
    public void ThrowIfRateLimited_RateLimitWithoutRetryHint_NullRetryAfter()
    {
        var result = CopilotWebSearchFunction.ParseSearchResult(
            "Rate limit exceeded. Please try later.");

        var ex = Assert.Throws<CopilotRateLimitException>(
            () => CopilotWebSearchFunction.ThrowIfRateLimited(result));

        Assert.Null(ex.RetryAfter);
    }

    [Fact]
    public void ThrowIfRateLimited_NormalResult_DoesNotThrow()
    {
        var json = """
        {
          "type": "output_text",
          "text": {
            "value": "Node.js 24 is the latest LTS.",
            "annotations": [
              {
                "text": "",
                "start_index": 0,
                "end_index": 10,
                "url_citation": {
                  "title": "Node.js",
                  "url": "https://nodejs.org"
                }
              }
            ]
          },
          "bing_searches": [
            { "text": "nodejs latest", "url": "https://www.bing.com/search?q=nodejs" }
          ]
        }
        """;

        var result = CopilotWebSearchFunction.ParseSearchResult(json);

        CopilotWebSearchFunction.ThrowIfRateLimited(result);
    }

    [Fact]
    public void ThrowIfRateLimited_ResultAboutRateLimiting_DoesNotThrow()
    {
        var json = """
        {
          "type": "output_text",
          "text": {
            "value": "Rate limiting is a technique used to control the number of requests a client can make to an API within a given time window.",
            "annotations": [
              {
                "text": "",
                "start_index": 100,
                "end_index": 112,
                "url_citation": {
                  "title": "API Rate Limiting Best Practices",
                  "url": "https://example.com/rate-limiting"
                }
              }
            ]
          },
          "bing_searches": [
            { "text": "API rate limiting", "url": "https://www.bing.com/search?q=API+rate+limiting" }
          ]
        }
        """;

        var result = CopilotWebSearchFunction.ParseSearchResult(json);

        CopilotWebSearchFunction.ThrowIfRateLimited(result);
    }

    [Fact]
    public void ParseRetryAfterFromText_ValidSeconds_ReturnsTimeSpan()
    {
        var retryAfter = CopilotRateLimitException.ParseRetryAfterFromText(
            "Rate limit exceeded (20 requests per minute). Try again in 42 seconds.");

        Assert.Equal(TimeSpan.FromSeconds(42), retryAfter);
    }

    [Fact]
    public void ParseRetryAfterFromText_NoHint_ReturnsNull()
    {
        var retryAfter = CopilotRateLimitException.ParseRetryAfterFromText(
            "Rate limit exceeded.");

        Assert.Null(retryAfter);
    }
}
