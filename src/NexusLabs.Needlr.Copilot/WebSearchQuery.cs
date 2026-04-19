namespace NexusLabs.Needlr.Copilot;

/// <summary>
/// Represents a Bing search query that the Copilot web search tool performed
/// while answering the user's question.
/// </summary>
public sealed class WebSearchQuery
{
    internal WebSearchQuery(string text, string url)
    {
        Text = text;
        Url = url;
    }

    /// <summary>
    /// The search query text that was sent to Bing.
    /// </summary>
    public string Text { get; }

    /// <summary>
    /// The Bing search URL for the query.
    /// </summary>
    public string Url { get; }
}
