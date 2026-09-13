using System;
using System.Text;

using NexusLabs.Needlr.Generators.Export;
using NexusLabs.Needlr.Generators.Models;

namespace NexusLabs.Needlr.Generators.CodeGen;

/// <summary>
/// Emits the analyzer-readable source-generated registration manifest.
/// </summary>
internal static class RegistrationManifestCodeGenerator
{
    private const int ManifestLiteralChunkLength = 2048;

    internal const string AssemblyMetadataKey =
        "NexusLabs.Needlr.RegistrationManifest";

    internal static string GenerateSource(
        GeneratedRegistrationPlan plan,
        BreadcrumbWriter breadcrumbs)
    {
        var builder = new StringBuilder();
        var json = RegistrationManifestSerializer.Serialize(plan);

        breadcrumbs.WriteFileHeader(
            builder,
            plan.AssemblyName,
            "Needlr Analyzer-Readable Registration Manifest");
        builder.AppendLine("#nullable enable");
        builder.AppendLine();
        builder.AppendLine(
            "[assembly: global::System.Reflection.AssemblyMetadataAttribute(");
        builder.AppendLine(
            $"    \"{AssemblyMetadataKey}\",");
        AppendManifestLiteral(builder, json);

        return builder.ToString();
    }

    private static void AppendManifestLiteral(
        StringBuilder builder,
        string json)
    {
        for (var offset = 0; offset < json.Length; offset += ManifestLiteralChunkLength)
        {
            var length = Math.Min(
                ManifestLiteralChunkLength,
                json.Length - offset);
            var chunk = json.Substring(offset, length);

            builder.Append("    \"");
            builder.Append(GeneratorHelpers.EscapeStringLiteral(chunk));
            builder.Append('"');
            builder.AppendLine(
                offset + length == json.Length
                    ? ")]"
                    : " +");
        }
    }
}
