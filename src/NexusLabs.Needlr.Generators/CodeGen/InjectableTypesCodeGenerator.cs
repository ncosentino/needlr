// Copyright (c) NexusLabs. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Linq;
using System.Text;

using NexusLabs.Needlr.Generators.Helpers;
using NexusLabs.Needlr.Generators.Models;

namespace NexusLabs.Needlr.Generators.CodeGen;

/// <summary>
/// Generates the static <c>_types</c> array of <see cref="DiscoveredType"/>
/// entries for the compile-time type registry.
/// </summary>
internal static class InjectableTypesCodeGenerator
{
    internal static void GenerateInjectableTypesArray(StringBuilder builder, IReadOnlyList<DiscoveredType> types, BreadcrumbWriter breadcrumbs, string? projectDirectory)
    {
        builder.AppendLine("    private static readonly InjectableTypeInfo[] _types =");
        builder.AppendLine("    [");

        var typesByAssembly = types.GroupBy(t => t.AssemblyName).OrderBy(g => g.Key);

        foreach (var group in typesByAssembly)
        {
            breadcrumbs.WriteInlineComment(builder, "        ", $"From {group.Key}");

            foreach (var type in group.OrderBy(t => t.TypeName))
            {
                // Write breadcrumb for this type
                if (breadcrumbs.Level == BreadcrumbLevel.Verbose)
                {
                    var sourcePath = type.SourceFilePath != null
                        ? BreadcrumbWriter.GetRelativeSourcePath(type.SourceFilePath, projectDirectory)
                        : $"[{type.AssemblyName}]";
                    var interfaces = type.InterfaceNames.Length > 0
                        ? string.Join(", ", type.InterfaceNames.Select(i => i.Split('.').Last()))
                        : "none";
                    var keysInfo = type.ServiceKeys.Length > 0
                        ? $"Keys: {string.Join(", ", type.ServiceKeys.Select(k => $"\"{k}\""))}"
                        : null;

                    if (keysInfo != null)
                    {
                        breadcrumbs.WriteVerboseBox(builder, "        ",
                            $"{type.TypeName.Split('.').Last()} → {interfaces}",
                            $"Source: {sourcePath}",
                            $"Lifetime: {type.Lifetime}",
                            keysInfo);
                    }
                    else
                    {
                        breadcrumbs.WriteVerboseBox(builder, "        ",
                            $"{type.TypeName.Split('.').Last()} → {interfaces}",
                            $"Source: {sourcePath}",
                            $"Lifetime: {type.Lifetime}");
                    }
                }

                builder.Append($"        new(typeof({type.TypeName}), ");

                // Interfaces
                if (type.InterfaceNames.Length == 0)
                {
                    builder.Append("Array.Empty<Type>(), ");
                }
                else
                {
                    builder.Append("[");
                    builder.Append(string.Join(", ", type.InterfaceNames.Select(i => $"typeof({i})")));
                    builder.Append("], ");
                }

                // Lifetime
                builder.Append($"InjectableLifetime.{type.Lifetime}, ");

                // Factory lambda - resolves dependencies and creates instance without reflection
                builder.Append("sp => new ");
                builder.Append(type.TypeName);
                builder.Append("(");
                if (type.ConstructorParameters.Length > 0)
                {
                    var parameterExpressions = type.ConstructorParameters
                        .Select(p => p.IsKeyed
                            ? $"sp.GetRequiredKeyedService<{p.TypeName}>(\"{GeneratorHelpers.EscapeStringLiteral(p.ServiceKey!)}\")"
                            : $"sp.GetRequiredService<{p.TypeName}>()");
                    builder.Append(string.Join(", ", parameterExpressions));
                }
                builder.Append("), ");

                // Service keys from [Keyed] attributes
                if (type.ServiceKeys.Length == 0)
                {
                    builder.AppendLine("Array.Empty<string>()),");
                }
                else
                {
                    builder.Append("[");
                    builder.Append(string.Join(", ", type.ServiceKeys.Select(k => $"\"{GeneratorHelpers.EscapeStringLiteral(k)}\"")));
                    builder.AppendLine("]),");
                }
            }
        }

        builder.AppendLine("    ];");
    }
}
