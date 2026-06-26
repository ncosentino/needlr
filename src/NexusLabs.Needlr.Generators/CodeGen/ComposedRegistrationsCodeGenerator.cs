// Copyright (c) NexusLabs. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Linq;
using System.Text;

using NexusLabs.Needlr.Generators.Models;

namespace NexusLabs.Needlr.Generators.CodeGen;

/// <summary>
/// Generates the <c>RegisterComposedTypes(IServiceCollection)</c> method that registers each open generic
/// composition type closed over the type argument(s) of every discovered implementation of its designated
/// open generic interface, exposed as the designated facade service type.
/// </summary>
internal static class ComposedRegistrationsCodeGenerator
{
    /// <summary>
    /// Emits the <c>RegisterComposedTypes(IServiceCollection)</c> method body into the TypeRegistry class.
    /// </summary>
    internal static void GenerateRegisterComposedTypesMethod(
        StringBuilder builder,
        IReadOnlyList<DiscoveredComposedRegistration> registrations,
        BreadcrumbWriter breadcrumbs,
        string? projectDirectory)
    {
        builder.AppendLine("    /// <summary>");
        builder.AppendLine("    /// Registers each composition type closed over the type argument(s) of every discovered");
        builder.AppendLine("    /// implementation of its designated open generic interface, exposed as the designated facade.");
        builder.AppendLine("    /// </summary>");
        builder.AppendLine("    /// <param name=\"services\">The service collection to register to.</param>");
        builder.AppendLine("    private static void RegisterComposedTypes(IServiceCollection services)");
        builder.AppendLine("    {");

        foreach (var registration in registrations)
        {
            var addMethod = GetAddMethod(registration.Lifetime);
            var closedShortName = registration.ClosedCompositionTypeName.Split('.').Last();
            var facadeShortName = registration.FacadeTypeName.Split('.').Last();
            var sourcePath = registration.SourceFilePath != null
                ? BreadcrumbWriter.GetRelativeSourcePath(registration.SourceFilePath, projectDirectory)
                : $"[{registration.AssemblyName}]";

            breadcrumbs.WriteInlineComment(builder, "        ", $"Composed: {closedShortName} as {facadeShortName} ← {sourcePath}");

            if (registration.ConstructorArguments.Count == 0)
            {
                builder.AppendLine($"        services.{addMethod}<{registration.FacadeTypeName}>(sp => new {registration.ClosedCompositionTypeName}());");
                continue;
            }

            builder.AppendLine($"        services.{addMethod}<{registration.FacadeTypeName}>(sp => new {registration.ClosedCompositionTypeName}(");

            for (var i = 0; i < registration.ConstructorArguments.Count; i++)
            {
                var isLast = i == registration.ConstructorArguments.Count - 1;
                builder.AppendLine($"            {registration.ConstructorArguments[i]}{(isLast ? "));" : ",")}");
            }
        }

        builder.AppendLine("    }");
    }

    private static string GetAddMethod(GeneratorLifetime lifetime) => lifetime switch
    {
        GeneratorLifetime.Scoped => "AddScoped",
        GeneratorLifetime.Transient => "AddTransient",
        _ => "AddSingleton",
    };
}
