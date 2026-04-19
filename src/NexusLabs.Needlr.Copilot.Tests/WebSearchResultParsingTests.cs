namespace NexusLabs.Needlr.Copilot.Tests;

public class WebSearchResultParsingTests
{
    [Fact]
    public void ParseSearchResult_FullResponse_ExtractsAllFields()
    {
        var json = """
        {
          "type": "output_text",
          "text": {
            "value": "Node.js 24 is the latest LTS release\u30103:0\u2020source\u3011.",
            "annotations": [
              {
                "text": "",
                "start_index": 37,
                "end_index": 49,
                "url_citation": {
                  "title": "Node.js Releases",
                  "url": "https://nodejs.org/en/blog/release"
                }
              }
            ]
          },
          "bing_searches": [
            {
              "text": "Node.js latest LTS",
              "url": "https://www.bing.com/search?q=Node.js+latest+LTS"
            }
          ],
          "annotations": null
        }
        """;

        var result = CopilotWebSearchFunction.ParseSearchResult(json);

        Assert.Equal(
            "Node.js 24 is the latest LTS release【3:0†source】.",
            result.Text);
        Assert.Equal(result.Text, result.ToString());

        Assert.Single(result.Citations);
        Assert.Equal("Node.js Releases", result.Citations[0].Title);
        Assert.Equal("https://nodejs.org/en/blog/release", result.Citations[0].Url);
        Assert.Equal(37, result.Citations[0].StartIndex);
        Assert.Equal(49, result.Citations[0].EndIndex);

        Assert.Single(result.SearchQueries);
        Assert.Equal("Node.js latest LTS", result.SearchQueries[0].Text);
        Assert.Equal(
            "https://www.bing.com/search?q=Node.js+latest+LTS",
            result.SearchQueries[0].Url);
    }

    [Fact]
    public void ParseSearchResult_MultipleCitations_ExtractsAll()
    {
        var json = """
        {
          "type": "output_text",
          "text": {
            "value": "Answer with two citations\u30103:0\u2020source\u3011\u30103:1\u2020source\u3011.",
            "annotations": [
              {
                "text": "",
                "start_index": 25,
                "end_index": 37,
                "url_citation": {
                  "title": "Source A",
                  "url": "https://example.com/a"
                }
              },
              {
                "text": "",
                "start_index": 37,
                "end_index": 49,
                "url_citation": {
                  "title": "Source B",
                  "url": "https://example.com/b"
                }
              }
            ]
          },
          "bing_searches": null,
          "annotations": null
        }
        """;

        var result = CopilotWebSearchFunction.ParseSearchResult(json);

        Assert.Equal(2, result.Citations.Count);
        Assert.Equal("Source A", result.Citations[0].Title);
        Assert.Equal("Source B", result.Citations[1].Title);
        Assert.Empty(result.SearchQueries);
    }

    [Fact]
    public void ParseSearchResult_NullAnnotations_ReturnsEmptyCitations()
    {
        var json = """
        {
          "type": "output_text",
          "text": {
            "value": "A generic answer with no web results.",
            "annotations": null
          },
          "bing_searches": null,
          "annotations": null
        }
        """;

        var result = CopilotWebSearchFunction.ParseSearchResult(json);

        Assert.Equal("A generic answer with no web results.", result.Text);
        Assert.Empty(result.Citations);
        Assert.Empty(result.SearchQueries);
    }

    [Fact]
    public void ParseSearchResult_MissingAnnotationsKey_ReturnsEmptyCitations()
    {
        var json = """
        {
          "type": "output_text",
          "text": {
            "value": "Answer without annotations key at all."
          }
        }
        """;

        var result = CopilotWebSearchFunction.ParseSearchResult(json);

        Assert.Equal("Answer without annotations key at all.", result.Text);
        Assert.Empty(result.Citations);
        Assert.Empty(result.SearchQueries);
    }

    [Fact]
    public void ParseSearchResult_PlainTextFallback_ReturnsRawText()
    {
        var plainText = "This is not JSON at all.";

        var result = CopilotWebSearchFunction.ParseSearchResult(plainText);

        Assert.Equal(plainText, result.Text);
        Assert.Empty(result.Citations);
        Assert.Empty(result.SearchQueries);
    }

    [Fact]
    public void ParseSearchResult_TextAsDirectString_ExtractsValue()
    {
        var json = """
        {
          "text": "Direct string, not an object."
        }
        """;

        var result = CopilotWebSearchFunction.ParseSearchResult(json);

        Assert.Equal("Direct string, not an object.", result.Text);
        Assert.Empty(result.Citations);
    }

    [Fact]
    public void ParseSearchResult_MalformedCitationIndex_DefaultsToZero()
    {
        var json = """
        {
          "type": "output_text",
          "text": {
            "value": "Answer text.",
            "annotations": [
              {
                "text": "",
                "start_index": "not_a_number",
                "end_index": 10,
                "url_citation": {
                  "title": "Bad Index",
                  "url": "https://example.com/bad"
                }
              },
              {
                "text": "",
                "start_index": 20,
                "end_index": 30,
                "url_citation": {
                  "title": "Good",
                  "url": "https://example.com/good"
                }
              }
            ]
          }
        }
        """;

        var result = CopilotWebSearchFunction.ParseSearchResult(json);

        Assert.Equal("Answer text.", result.Text);
        Assert.Equal(2, result.Citations.Count);
        Assert.Equal("Bad Index", result.Citations[0].Title);
        Assert.Equal(0, result.Citations[0].StartIndex);
        Assert.Equal(10, result.Citations[0].EndIndex);
        Assert.Equal("Good", result.Citations[1].Title);
        Assert.Equal(20, result.Citations[1].StartIndex);
    }

    [Fact]
    public void ParseSearchResult_AnnotationWithoutUrlCitation_Skipped()
    {
        var json = """
        {
          "type": "output_text",
          "text": {
            "value": "Answer.",
            "annotations": [
              {
                "text": "",
                "start_index": 0,
                "end_index": 5
              }
            ]
          }
        }
        """;

        var result = CopilotWebSearchFunction.ParseSearchResult(json);

        Assert.Equal("Answer.", result.Text);
        Assert.Empty(result.Citations);
    }

    [Fact]
    public void ParseSearchResult_RootAnnotationsFallback_UsedWhenTextAnnotationsAbsent()
    {
        var json = """
        {
          "type": "output_text",
          "text": {
            "value": "Answer from root annotations."
          },
          "annotations": [
            {
              "text": "",
              "start_index": 0,
              "end_index": 10,
              "url_citation": {
                "title": "Root Source",
                "url": "https://example.com/root"
              }
            }
          ]
        }
        """;

        var result = CopilotWebSearchFunction.ParseSearchResult(json);

        Assert.Single(result.Citations);
        Assert.Equal("Root Source", result.Citations[0].Title);
    }

    [Fact]
    public void ParseSearchResult_MultipleBingSearches_ExtractsAll()
    {
        var json = """
        {
          "type": "output_text",
          "text": {
            "value": "Answer."
          },
          "bing_searches": [
            {
              "text": "query one",
              "url": "https://www.bing.com/search?q=one"
            },
            {
              "text": "query two",
              "url": "https://www.bing.com/search?q=two"
            }
          ]
        }
        """;

        var result = CopilotWebSearchFunction.ParseSearchResult(json);

        Assert.Equal(2, result.SearchQueries.Count);
        Assert.Equal("query one", result.SearchQueries[0].Text);
        Assert.Equal("query two", result.SearchQueries[1].Text);
    }

    [Fact]
    public void WebSearchResult_ToString_ReturnsText()
    {
        var result = CopilotWebSearchFunction.ParseSearchResult("""
        {
          "type": "output_text",
          "text": { "value": "The answer." }
        }
        """);

        Assert.Equal("The answer.", result.ToString());
    }
}
