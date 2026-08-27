using System.Text;

using Microsoft.CodeAnalysis.Text;

namespace NexusLabs.Needlr.Generators;

/// <summary>
/// Creates <see cref="SourceText"/> for <c>AddSource</c> with line endings normalized
/// to LF.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="StringBuilder.AppendLine()"/> emits <see cref="System.Environment.NewLine"/>,
/// which is CRLF on Windows and LF elsewhere. Generated source therefore differed
/// byte-for-byte between a Windows build and a Linux build of identical input.
/// </para>
/// <para>
/// Normalizing here rather than at the emitters is deliberate: there are roughly 1,272
/// <c>AppendLine</c> call sites and 16 <c>AddSource</c> boundaries. Every emitter routes
/// through this one choke point, so an emitter added later inherits the guarantee
/// without knowing about it.
/// </para>
/// </remarks>
internal static class GeneratedSourceText
{
    /// <summary>
    /// Normalizes line endings and wraps the result as UTF-8 <see cref="SourceText"/>.
    /// </summary>
    internal static SourceText Create(string generatedText)
    {
        // The only sanctioned SourceText.From in this project. BannedSymbols.txt routes
        // every other emitter here so normalization cannot be bypassed.
#pragma warning disable RS0030
        return SourceText.From(NormalizeLineEndings(generatedText), Encoding.UTF8);
#pragma warning restore RS0030
    }

    private static string NormalizeLineEndings(string text)
    {
        if (text.IndexOf('\r') < 0)
        {
            return text;
        }

        return text
            .Replace("\r\n", "\n")
            .Replace("\r", "\n");
    }
}
