namespace SupportTriagePipelineApp;

/// <summary>
/// Typed pipeline state shared across all phases via
/// <c>GetRequiredState&lt;TriageState&gt;()</c>. Nullable urgency
/// distinguishes "not yet classified" from "low urgency."
/// </summary>
internal sealed class TriageState
{
    /// <summary>Urgency score (1-5), or null if classification hasn't run or parsing failed.</summary>
    public int? UrgencyScore { get; set; }

    /// <summary>
    /// Parse urgency from agent response text. Uses multiple strategies:
    /// exact format match, keyword detection, numeric scale detection.
    /// </summary>
    public bool TryParseUrgency(string? responseText)
    {
        if (responseText is null)
        {
            return false;
        }

        var upper = responseText.ToUpperInvariant();

        if (upper.Contains("CRITICAL") || upper.Contains("5/5") || upper.Contains("10/10") || upper.Contains("9/10"))
        {
            UrgencyScore = 5;
            return true;
        }

        if (upper.Contains("HIGH") || upper.Contains("8/10") || upper.Contains("7/10") || upper.Contains("4/5"))
        {
            UrgencyScore = 4;
            return true;
        }

        if (upper.Contains("MODERATE") || upper.Contains("MEDIUM") || upper.Contains("6/10") || upper.Contains("5/10") || upper.Contains("3/5"))
        {
            UrgencyScore = 3;
            return true;
        }

        if (upper.Contains("MINOR") || upper.Contains("4/10") || upper.Contains("3/10") || upper.Contains("2/5"))
        {
            UrgencyScore = 2;
            return true;
        }

        if (upper.Contains("LOW") || upper.Contains("2/10") || upper.Contains("1/10") || upper.Contains("1/5"))
        {
            UrgencyScore = 1;
            return true;
        }

        return false;
    }
}
