using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;

using Microsoft.CodeAnalysis;

namespace NexusLabs.Needlr.Generators;

/// <summary>
/// Extracts normalized semantic content from Roslyn XML documentation comments.
/// </summary>
internal static class DocumentationCommentHelper
{
    /// <summary>
    /// Gets the semantic content of a named <c>param</c> element.
    /// </summary>
    internal static string? GetParameterDocumentation(
        ISymbol symbol,
        string parameterName)
    {
        var document = TryParseDocumentation(symbol);
        var parameter = document?
            .Descendants("param")
            .FirstOrDefault(element =>
                element.Attribute("name")?.Value == parameterName);
        return parameter is null
            ? null
            : GetNormalizedElementContent(parameter);
    }

    /// <summary>
    /// Gets the semantic content of a symbol's <c>summary</c> element.
    /// </summary>
    internal static string? GetSummaryDocumentation(ISymbol symbol)
    {
        var summary = TryParseDocumentation(symbol)?
            .Descendants("summary")
            .FirstOrDefault();
        return summary is null
            ? null
            : GetNormalizedElementContent(summary);
    }

    private static XDocument? TryParseDocumentation(ISymbol symbol)
    {
        var xml = symbol.GetDocumentationCommentXml();
        if (string.IsNullOrWhiteSpace(xml))
            return null;

        try
        {
            return XDocument.Parse(xml);
        }
        catch (XmlException)
        {
            return null;
        }
    }

    private static string? GetNormalizedElementContent(XElement element)
    {
        foreach (var text in element.DescendantNodesAndSelf().OfType<XText>())
        {
            text.Value = NormalizeWhitespace(text.Value);
        }

        var content = string.Concat(
                element.Nodes()
                    .Select(node => node.ToString(SaveOptions.DisableFormatting)))
            .Trim();
        return content.Length == 0 ? null : content;
    }

    private static string NormalizeWhitespace(string value)
    {
        var builder = new StringBuilder(value.Length);
        var previousWasWhitespace = false;

        foreach (var character in value)
        {
            if (char.IsWhiteSpace(character))
            {
                if (!previousWasWhitespace)
                    builder.Append(' ');

                previousWasWhitespace = true;
                continue;
            }

            builder.Append(character);
            previousWasWhitespace = false;
        }

        return builder.ToString();
    }
}
