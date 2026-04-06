// Copyright (c) NexusLabs. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Linq;
using System.Text;

using NexusLabs.Needlr.Generators.Models;

namespace NexusLabs.Needlr.Generators.CodeGen;

/// <summary>
/// Generates the static <c>_plugins</c> array of <see cref="DiscoveredPlugin"/>
/// entries for the compile-time type registry.
/// </summary>
internal static class PluginsCodeGenerator
{
    internal static void GeneratePluginTypesArray(StringBuilder builder, IReadOnlyList<DiscoveredPlugin> plugins, BreadcrumbWriter breadcrumbs, string? projectDirectory)
    {
        builder.AppendLine("    private static readonly PluginTypeInfo[] _plugins =");
        builder.AppendLine("    [");

        // Sort plugins by Order first, then by TypeName for determinism
        var sortedPlugins = plugins
            .OrderBy(p => p.Order)
            .ThenBy(p => p.TypeName, StringComparer.Ordinal)
            .ToList();

        // Group for breadcrumb display, but maintain the sorted order
        var pluginsByAssembly = sortedPlugins.GroupBy(p => p.AssemblyName).OrderBy(g => g.Key);

        foreach (var group in pluginsByAssembly)
        {
            breadcrumbs.WriteInlineComment(builder, "        ", $"From {group.Key}");

            // Maintain order within assembly group
            foreach (var plugin in group.OrderBy(p => p.Order).ThenBy(p => p.TypeName, StringComparer.Ordinal))
            {
                // Write verbose breadcrumb for this plugin
                if (breadcrumbs.Level == BreadcrumbLevel.Verbose)
                {
                    var sourcePath = plugin.SourceFilePath != null
                        ? BreadcrumbWriter.GetRelativeSourcePath(plugin.SourceFilePath, projectDirectory)
                        : $"[{plugin.AssemblyName}]";
                    var interfaces = plugin.InterfaceNames.Length > 0
                        ? string.Join(", ", plugin.InterfaceNames.Select(i => i.Split('.').Last()))
                        : "none";
                    var orderInfo = plugin.Order != 0 ? $"Order: {plugin.Order}" : "Order: 0 (default)";

                    breadcrumbs.WriteVerboseBox(builder, "        ",
                        $"Plugin: {plugin.TypeName.Split('.').Last()}",
                        $"Source: {sourcePath}",
                        $"Implements: {interfaces}",
                        orderInfo);
                }
                else if (breadcrumbs.Level == BreadcrumbLevel.Minimal && plugin.Order != 0)
                {
                    // Show order in minimal mode only if non-default
                    breadcrumbs.WriteInlineComment(builder, "        ", $"{plugin.TypeName.Split('.').Last()} (Order: {plugin.Order})");
                }

                builder.Append($"        new(typeof({plugin.TypeName}), ");

                // Interfaces
                if (plugin.InterfaceNames.Length == 0)
                {
                    builder.Append("Array.Empty<Type>(), ");
                }
                else
                {
                    builder.Append("[");
                    builder.Append(string.Join(", ", plugin.InterfaceNames.Select(i => $"typeof({i})")));
                    builder.Append("], ");
                }

                // Factory lambda - no Activator.CreateInstance needed
                builder.Append($"() => new {plugin.TypeName}(), ");

                // Attributes
                if (plugin.AttributeNames.Length == 0)
                {
                    builder.Append("Array.Empty<Type>(), ");
                }
                else
                {
                    builder.Append("[");
                    builder.Append(string.Join(", ", plugin.AttributeNames.Select(a => $"typeof({a})")));
                    builder.Append("], ");
                }

                // Order
                builder.AppendLine($"{plugin.Order}),");
            }
        }

        builder.AppendLine("    ];");
    }
}
