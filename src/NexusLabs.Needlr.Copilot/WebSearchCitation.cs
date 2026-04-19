namespace NexusLabs.Needlr.Copilot;

/// <summary>
/// Represents a citation from a Copilot web search result, linking a
/// span of the answer text to a source URL.
/// </summary>
/// <remarks>
/// <see cref="StartIndex"/> and <see cref="EndIndex"/> are character offsets
/// into <see cref="WebSearchResult.Text"/> exactly as returned by the API.
/// They correspond to the <c>【3:N†source】</c> markers in the raw text.
/// </remarks>
public sealed class WebSearchCitation
{
    internal WebSearchCitation(
        string title,
        string url,
        int startIndex,
        int endIndex)
    {
        Title = title;
        Url = url;
        StartIndex = startIndex;
        EndIndex = endIndex;
    }

    /// <summary>
    /// The title of the cited source page.
    /// </summary>
    public string Title { get; }

    /// <summary>
    /// The URL of the cited source.
    /// </summary>
    public string Url { get; }

    /// <summary>
    /// The start character offset (inclusive) in
    /// <see cref="WebSearchResult.Text"/> where this citation marker appears.
    /// </summary>
    public int StartIndex { get; }

    /// <summary>
    /// The end character offset (exclusive) in
    /// <see cref="WebSearchResult.Text"/> where this citation marker ends.
    /// </summary>
    public int EndIndex { get; }
}
