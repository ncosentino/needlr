using System.ComponentModel;
using System.Text.Json;

using NexusLabs.Needlr.AgentFramework;

namespace SimpleAgentFrameworkApp.Agents;

/// <summary>
/// Demonstrates array-of-objects parameters with source-generated JSON schema.
/// <see cref="RecordFeedback"/> accepts a <see cref="FeedbackEntry"/>[] array,
/// which the source generator must emit a complete JSON schema for — including
/// the object's properties (<c>topic</c>, <c>rating</c>, <c>comment</c>) and
/// required fields. Without proper schema generation, the LLM would receive
/// only <c>{"type":"array"}</c> with no items definition and would not know
/// what shape to send.
/// </summary>
[AgentFunctionGroup("geography")]
internal sealed class BatchFunctions
{
    private readonly List<FeedbackEntry> _feedback = [];

    /// <summary>
    /// Records feedback entries in batch. Each entry has a topic, rating, and comment.
    /// </summary>
    [AgentFunction]
    [Description("Records one or more feedback entries about topics discussed. Use this to rate and comment on topics.")]
    public string RecordFeedback(
        [Description("Array of feedback entries, each with a topic name, numeric rating (1-5), and optional comment.")]
        FeedbackEntry[] entries)
    {
        _feedback.AddRange(entries);
        return $"Recorded {entries.Length} feedback entry/entries. Total: {_feedback.Count}.";
    }

    [AgentFunction]
    [Description("Returns all recorded feedback entries.")]
    public FeedbackEntry[] GetAllFeedback() => _feedback.ToArray();

    /// <summary>
    /// Exercises primitive-alias array parameters (<c>string[]</c>, <c>int[]</c>) through
    /// the source generator. Regression coverage for the generator bug where
    /// <c>SymbolDisplayFormat.FullyQualifiedFormat</c> emitted <c>global::string</c> /
    /// <c>global::int</c> — which is invalid C# — instead of <c>global::System.String</c> /
    /// <c>global::System.Int32</c>.
    /// </summary>
    [AgentFunction]
    [Description("Tags a set of topics with integer priority scores. Demonstrates primitive array parameters.")]
    public string TagTopics(
        [Description("Array of topic names to tag.")]
        string[] topics,
        [Description("Array of priority scores (1-10) corresponding to each topic.")]
        int[] priorities)
    {
        return $"Tagged {topics.Length} topic(s) with {priorities.Length} priority score(s).";
    }

    /// <summary>
    /// Demonstrates the <see cref="JsonElement"/> parameter pattern — the analyzer-recommended
    /// alternative (NDLRMAF030) when a tool wants direct, typed access to arbitrary JSON the
    /// model produces. Compare to a hypothetical <c>string payloadJson</c> shape: with
    /// <see cref="JsonElement"/> the tool body inspects <see cref="JsonElement.ValueKind"/>
    /// directly without re-parsing canonical text.
    /// </summary>
    [AgentFunction]
    [Description("Stores arbitrary JSON metadata for a topic. Pass a JSON object describing the topic.")]
    public string AttachTopicMetadata(
        [Description("Topic identifier.")]
        string topicId,
        [Description("Arbitrary JSON object holding metadata fields.")]
        JsonElement metadata)
    {
        if (metadata.ValueKind != JsonValueKind.Object)
        {
            return $"Metadata for '{topicId}' was not a JSON object (received {metadata.ValueKind}).";
        }

        var fieldCount = metadata.EnumerateObject().Count();
        return $"Attached {fieldCount} metadata field(s) to topic '{topicId}'.";
    }
}

/// <summary>
/// Complex type used as an array element to validate source-generator JSON schema emission.
/// The generator must produce: <c>{"type":"object","properties":{"topic":{"type":"string",...},"rating":{"type":"integer",...},"comment":{"type":"string",...}},"required":["topic","rating"]}</c>.
/// </summary>
public sealed class FeedbackEntry
{
    [Description("The topic being rated.")]
    public string Topic { get; set; } = "";

    [Description("Numeric rating from 1 to 5.")]
    public int Rating { get; set; }

    [Description("Optional comment about the topic.")]
    public string? Comment { get; set; }
}
