using System.Text.RegularExpressions;

namespace NexusLabs.Needlr.AgentFramework.Testing;

/// <summary>
/// Assertion helpers for verifying prompt integrity without LLM invocation.
/// Use in unit tests to catch prompt regressions (missing safety rules, deleted
/// sections, forbidden patterns) at zero token cost.
/// </summary>
/// <example>
/// <code>
/// [Fact]
/// public void WriterPrompt_HasSafetyRules()
/// {
///     PromptAssert.Contains(WriterPrompt.Text, "ABSOLUTE RULE");
///     PromptAssert.ContainsInSection(WriterPrompt.Text, "### Critical", "meta-instruction-leak");
///     PromptAssert.ForbidsPattern(WriterPrompt.Text, @"TODO|FIXME|HACK");
/// }
/// </code>
/// </example>
public static class PromptAssert
{
    /// <summary>
    /// Asserts that <paramref name="prompt"/> contains <paramref name="expected"/>
    /// using <see cref="StringComparison.OrdinalIgnoreCase"/>.
    /// </summary>
    /// <param name="prompt">The prompt text to inspect.</param>
    /// <param name="expected">The substring that must be present.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="prompt"/> is <see langword="null"/> or
    /// <paramref name="expected"/> is <see langword="null"/> or empty.
    /// </exception>
    /// <exception cref="PromptAssertionException">The expected text was not found.</exception>
    public static void Contains(string prompt, string expected)
        => Contains(prompt, expected, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Asserts that <paramref name="prompt"/> contains <paramref name="expected"/>
    /// using the specified <paramref name="comparison"/>.
    /// </summary>
    /// <param name="prompt">The prompt text to inspect.</param>
    /// <param name="expected">The substring that must be present.</param>
    /// <param name="comparison">The comparison rule to use.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="prompt"/> is <see langword="null"/> or
    /// <paramref name="expected"/> is <see langword="null"/> or empty.
    /// </exception>
    /// <exception cref="PromptAssertionException">The expected text was not found.</exception>
    public static void Contains(string prompt, string expected, StringComparison comparison)
    {
        ValidatePrompt(prompt);
        ValidateExpected(expected);

        if (!prompt.Contains(expected, comparison))
        {
            throw new PromptAssertionException(
                $"Expected prompt to contain '{expected}' but it was not found. " +
                $"Prompt length: {prompt.Length} chars.");
        }
    }

    /// <summary>
    /// Asserts that <paramref name="prompt"/> does NOT contain <paramref name="forbidden"/>
    /// using <see cref="StringComparison.OrdinalIgnoreCase"/>.
    /// </summary>
    /// <param name="prompt">The prompt text to inspect.</param>
    /// <param name="forbidden">The substring that must be absent.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="prompt"/> is <see langword="null"/> or
    /// <paramref name="forbidden"/> is <see langword="null"/> or empty.
    /// </exception>
    /// <exception cref="PromptAssertionException">The forbidden text was found.</exception>
    public static void DoesNotContain(string prompt, string forbidden)
    {
        ValidatePrompt(prompt);
        ValidateExpected(forbidden, nameof(forbidden));

        var index = prompt.IndexOf(forbidden, StringComparison.OrdinalIgnoreCase);
        if (index >= 0)
        {
            var context = ExtractContext(prompt, index, forbidden.Length);
            throw new PromptAssertionException(
                $"Expected prompt to NOT contain '{forbidden}' but it was found at offset {index}. " +
                $"Context: '...{context}...'");
        }
    }

    /// <summary>
    /// Asserts that <paramref name="prompt"/> does not match <paramref name="regexPattern"/>.
    /// </summary>
    /// <param name="prompt">The prompt text to inspect.</param>
    /// <param name="regexPattern">A regular expression pattern that must NOT match.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="prompt"/> is <see langword="null"/> or
    /// <paramref name="regexPattern"/> is <see langword="null"/> or empty.
    /// </exception>
    /// <exception cref="PromptAssertionException">The pattern matched.</exception>
    public static void ForbidsPattern(string prompt, string regexPattern)
    {
        ValidatePrompt(prompt);
        ValidateExpected(regexPattern, nameof(regexPattern));

        var match = Regex.Match(prompt, regexPattern, RegexOptions.None);
        if (match.Success)
        {
            throw new PromptAssertionException(
                $"Expected prompt to not match pattern '{regexPattern}' but found match: " +
                $"'{match.Value}' at offset {match.Index}.");
        }
    }

    /// <summary>
    /// Asserts that <paramref name="prompt"/> contains a markdown section header
    /// matching <paramref name="sectionHeader"/> (e.g., <c>"### Critical"</c>).
    /// </summary>
    /// <param name="prompt">The prompt text to inspect.</param>
    /// <param name="sectionHeader">The markdown header text (including <c>#</c> prefix).</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="prompt"/> is <see langword="null"/> or
    /// <paramref name="sectionHeader"/> is <see langword="null"/> or empty.
    /// </exception>
    /// <exception cref="PromptAssertionException">The section header was not found.</exception>
    public static void HasSection(string prompt, string sectionHeader)
    {
        ValidatePrompt(prompt);
        ValidateExpected(sectionHeader, nameof(sectionHeader));

        if (FindSectionStart(prompt, sectionHeader) < 0)
        {
            throw new PromptAssertionException(
                $"Expected prompt to contain section '{sectionHeader}' but it was not found.");
        }
    }

    /// <summary>
    /// Asserts that <paramref name="expected"/> appears within the markdown section
    /// that starts at <paramref name="sectionHeader"/>. The section ends at the next
    /// header of equal or higher level, or at the end of the prompt.
    /// </summary>
    /// <param name="prompt">The prompt text to inspect.</param>
    /// <param name="sectionHeader">The markdown header that starts the section (e.g., <c>"### Critical"</c>).</param>
    /// <param name="expected">The text that must appear inside the section.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="prompt"/> is <see langword="null"/> or
    /// <paramref name="sectionHeader"/> or <paramref name="expected"/> is <see langword="null"/> or empty.
    /// </exception>
    /// <exception cref="PromptAssertionException">
    /// The section was not found, or the expected text was not in the section.
    /// </exception>
    public static void ContainsInSection(string prompt, string sectionHeader, string expected)
    {
        ValidatePrompt(prompt);
        ValidateExpected(sectionHeader, nameof(sectionHeader));
        ValidateExpected(expected);

        var sectionStart = FindSectionStart(prompt, sectionHeader);
        if (sectionStart < 0)
        {
            throw new PromptAssertionException(
                $"Section '{sectionHeader}' not found in prompt.");
        }

        var sectionContent = ExtractSectionContent(prompt, sectionHeader, sectionStart);

        if (!sectionContent.Contains(expected, StringComparison.OrdinalIgnoreCase))
        {
            var preview = sectionContent.Length > 200
                ? sectionContent[..200]
                : sectionContent;
            throw new PromptAssertionException(
                $"Expected section '{sectionHeader}' to contain '{expected}' but it was not found. " +
                $"Section content: '{preview}'");
        }
    }

    /// <summary>
    /// Asserts that <paramref name="firstSection"/> appears before
    /// <paramref name="secondSection"/> in <paramref name="prompt"/>.
    /// </summary>
    /// <param name="prompt">The prompt text to inspect.</param>
    /// <param name="firstSection">The section header that should come first.</param>
    /// <param name="secondSection">The section header that should come second.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="prompt"/> is <see langword="null"/> or
    /// <paramref name="firstSection"/> or <paramref name="secondSection"/> is <see langword="null"/> or empty.
    /// </exception>
    /// <exception cref="PromptAssertionException">
    /// A section was not found, or the order was reversed.
    /// </exception>
    public static void SectionOrder(string prompt, string firstSection, string secondSection)
    {
        ValidatePrompt(prompt);
        ValidateExpected(firstSection, nameof(firstSection));
        ValidateExpected(secondSection, nameof(secondSection));

        var idx1 = FindSectionStart(prompt, firstSection);
        if (idx1 < 0)
        {
            throw new PromptAssertionException(
                $"Expected prompt to contain section '{firstSection}' but it was not found.");
        }

        var idx2 = FindSectionStart(prompt, secondSection);
        if (idx2 < 0)
        {
            throw new PromptAssertionException(
                $"Expected prompt to contain section '{secondSection}' but it was not found.");
        }

        if (idx1 >= idx2)
        {
            throw new PromptAssertionException(
                $"Expected section '{firstSection}' to appear before '{secondSection}' " +
                $"but order was reversed (first at offset {idx1}, second at offset {idx2}).");
        }
    }

    private static void ValidatePrompt(string prompt)
    {
        if (prompt is null)
        {
            throw new ArgumentException("Prompt cannot be null.", nameof(prompt));
        }
    }

    private static void ValidateExpected(string value, string paramName = "expected")
    {
        if (string.IsNullOrEmpty(value))
        {
            throw new ArgumentException("Value cannot be null or empty.", paramName);
        }
    }

    private static string ExtractContext(string prompt, int matchIndex, int matchLength)
    {
        const int contextRadius = 20;
        var start = Math.Max(0, matchIndex - contextRadius);
        var end = Math.Min(prompt.Length, matchIndex + matchLength + contextRadius);
        return prompt[start..end];
    }

    /// <summary>
    /// Finds the start index of a markdown section header line.
    /// The header must appear at the start of a line.
    /// </summary>
    private static int FindSectionStart(string prompt, string sectionHeader)
    {
        // Check if the prompt starts with the header
        if (prompt.StartsWith(sectionHeader, StringComparison.OrdinalIgnoreCase))
        {
            // Verify it's a complete line match (next char is newline or end of string)
            if (prompt.Length == sectionHeader.Length ||
                prompt[sectionHeader.Length] == '\n' ||
                prompt[sectionHeader.Length] == '\r')
            {
                return 0;
            }
        }

        // Search for the header at the start of a line
        var searchOffset = 0;
        while (searchOffset < prompt.Length)
        {
            var idx = prompt.IndexOf(sectionHeader, searchOffset, StringComparison.OrdinalIgnoreCase);
            if (idx < 0)
            {
                return -1;
            }

            // Must be at the start of a line
            if (idx == 0 || prompt[idx - 1] == '\n')
            {
                // Must be followed by newline, end of string, or whitespace to be a header
                var endIdx = idx + sectionHeader.Length;
                if (endIdx >= prompt.Length ||
                    prompt[endIdx] == '\n' ||
                    prompt[endIdx] == '\r')
                {
                    return idx;
                }
            }

            searchOffset = idx + 1;
        }

        return -1;
    }

    /// <summary>
    /// Extracts the content of a section starting after the header line,
    /// ending at the next header of same or higher level, or end of string.
    /// </summary>
    private static string ExtractSectionContent(string prompt, string sectionHeader, int sectionStart)
    {
        var headerLevel = CountHeaderLevel(sectionHeader);

        // Find the end of the header line
        var contentStart = prompt.IndexOf('\n', sectionStart);
        if (contentStart < 0)
        {
            return string.Empty;
        }

        contentStart++; // skip past the newline

        // Scan forward for the next header of same or higher level
        var lines = prompt[contentStart..].Split('\n');
        var sectionEnd = contentStart;
        foreach (var line in lines)
        {
            var trimmed = line.TrimStart();
            if (trimmed.StartsWith('#'))
            {
                var lineLevel = CountHeaderLevel(trimmed);
                if (lineLevel <= headerLevel)
                {
                    break;
                }
            }

            sectionEnd += line.Length + 1; // +1 for the newline
        }

        // Clamp to prompt length
        sectionEnd = Math.Min(sectionEnd, prompt.Length);

        return prompt[contentStart..sectionEnd];
    }

    private static int CountHeaderLevel(string header)
    {
        var count = 0;
        foreach (var ch in header)
        {
            if (ch == '#')
            {
                count++;
            }
            else
            {
                break;
            }
        }

        return count;
    }
}
