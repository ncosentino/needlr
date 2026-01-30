// Copyright (c) NexusLabs. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using NexusLabs.Needlr.Generators.Models;

namespace NexusLabs.Needlr.Generators.CodeGen;

/// <summary>
/// Generates factory interfaces and implementations for [GenerateFactory] attributed types.
/// </summary>
internal static class FactoryCodeGenerator
{
    internal static void GenerateFactoryInterface(StringBuilder builder, DiscoveredFactory factory, BreadcrumbWriter breadcrumbs, string? projectDirectory)
    {
        var factoryName = $"I{factory.SimpleTypeName}Factory";

        builder.AppendLine("/// <summary>");
        builder.AppendLine($"/// Factory interface for creating instances of <see cref=\"{factory.TypeName}\"/>.");
        builder.AppendLine("/// </summary>");
        builder.AppendLine("[global::System.CodeDom.Compiler.GeneratedCodeAttribute(\"NexusLabs.Needlr.Generators\", \"1.0.0\")]");
        builder.AppendLine($"public interface {factoryName}");
        builder.AppendLine("{");

        // Generate Create method for each constructor
        foreach (var ctor in factory.Constructors)
        {
            var runtimeParamList = string.Join(", ", ctor.RuntimeParameters.Select(p => 
            {
                var simpleTypeName = GeneratorHelpers.GetSimpleTypeName(p.TypeName);
                return $"{p.TypeName} {p.ParameterName ?? GeneratorHelpers.ToCamelCase(simpleTypeName)}";
            }));

            builder.AppendLine($"    /// <summary>Creates a new instance of {factory.SimpleTypeName}.</summary>");
            
            // Add <param> tags for documented runtime parameters
            foreach (var param in ctor.RuntimeParameters)
            {
                if (!string.IsNullOrWhiteSpace(param.DocumentationComment))
                {
                    var paramName = param.ParameterName ?? GeneratorHelpers.ToCamelCase(GeneratorHelpers.GetSimpleTypeName(param.TypeName));
                    var escapedDoc = GeneratorHelpers.EscapeXmlContent(param.DocumentationComment!);
                    builder.AppendLine($"    /// <param name=\"{paramName}\">{escapedDoc}</param>");
                }
            }
            
            builder.AppendLine($"    {factory.ReturnTypeName} Create({runtimeParamList});");
        }

        builder.AppendLine("}");
    }

    internal static void GenerateFactoryImplementation(StringBuilder builder, DiscoveredFactory factory, BreadcrumbWriter breadcrumbs, string? projectDirectory)
    {
        var factoryInterfaceName = $"I{factory.SimpleTypeName}Factory";
        var factoryImplName = $"{factory.SimpleTypeName}Factory";

        // Collect all unique injectable parameters across all constructors
        var allInjectableParams = factory.Constructors
            .SelectMany(c => c.InjectableParameters)
            .GroupBy(p => p.TypeName)
            .Select(g => g.First())
            .ToList();

        builder.AppendLine("/// <summary>");
        builder.AppendLine($"/// Factory implementation for creating instances of <see cref=\"{factory.TypeName}\"/>.");
        builder.AppendLine("/// </summary>");
        builder.AppendLine("[global::System.CodeDom.Compiler.GeneratedCodeAttribute(\"NexusLabs.Needlr.Generators\", \"1.0.0\")]");
        builder.AppendLine($"internal sealed class {factoryImplName} : {factoryInterfaceName}");
        builder.AppendLine("{");

        // Fields for injectable dependencies
        foreach (var param in allInjectableParams)
        {
            var fieldName = "_" + GeneratorHelpers.ToCamelCase(GeneratorHelpers.GetSimpleTypeName(param.TypeName));
            builder.AppendLine($"    private readonly {param.TypeName} {fieldName};");
        }

        builder.AppendLine();

        // Constructor
        var ctorParams = string.Join(", ", allInjectableParams.Select(p => $"{p.TypeName} {GeneratorHelpers.ToCamelCase(GeneratorHelpers.GetSimpleTypeName(p.TypeName))}"));
        builder.AppendLine($"    public {factoryImplName}({ctorParams})");
        builder.AppendLine("    {");
        foreach (var param in allInjectableParams)
        {
            var fieldName = "_" + GeneratorHelpers.ToCamelCase(GeneratorHelpers.GetSimpleTypeName(param.TypeName));
            var paramName = GeneratorHelpers.ToCamelCase(GeneratorHelpers.GetSimpleTypeName(param.TypeName));
            builder.AppendLine($"        {fieldName} = {paramName};");
        }
        builder.AppendLine("    }");
        builder.AppendLine();

        // Create methods for each constructor
        foreach (var ctor in factory.Constructors)
        {
            var runtimeParamList = string.Join(", ", ctor.RuntimeParameters.Select(p => 
            {
                var paramName = p.ParameterName ?? GeneratorHelpers.ToCamelCase(GeneratorHelpers.GetSimpleTypeName(p.TypeName));
                return $"{p.TypeName} {paramName}";
            }));

            builder.AppendLine($"    public {factory.ReturnTypeName} Create({runtimeParamList})");
            builder.AppendLine("    {");
            builder.Append($"        return new {factory.TypeName}(");

            // Build constructor arguments - injectable first (from fields), then runtime
            var allArgs = new List<string>();
            foreach (var inj in ctor.InjectableParameters)
            {
                var fieldName = "_" + GeneratorHelpers.ToCamelCase(GeneratorHelpers.GetSimpleTypeName(inj.TypeName));
                allArgs.Add(fieldName);
            }
            foreach (var rt in ctor.RuntimeParameters)
            {
                var paramName = rt.ParameterName ?? GeneratorHelpers.ToCamelCase(GeneratorHelpers.GetSimpleTypeName(rt.TypeName));
                allArgs.Add(paramName);
            }

            builder.Append(string.Join(", ", allArgs));
            builder.AppendLine(");");
            builder.AppendLine("    }");
        }

        builder.AppendLine("}");
    }

    internal static void GenerateFuncRegistration(StringBuilder builder, DiscoveredFactory factory, FactoryDiscoveryHelper.FactoryConstructorInfo ctor, string indent)
    {
        // Build Func<TRuntime..., TReturn> type - uses ReturnTypeName (interface if generic attribute used)
        var runtimeTypes = string.Join(", ", ctor.RuntimeParameters.Select(p => p.TypeName));
        var funcType = $"Func<{runtimeTypes}, {factory.ReturnTypeName}>";

        // Build the lambda
        var runtimeParams = string.Join(", ", ctor.RuntimeParameters.Select(p => 
            p.ParameterName ?? GeneratorHelpers.ToCamelCase(GeneratorHelpers.GetSimpleTypeName(p.TypeName))));

        builder.AppendLine($"{indent}services.AddSingleton<{funcType}>(sp =>");
        builder.AppendLine($"{indent}    ({runtimeParams}) => new {factory.TypeName}(");

        // Build constructor call arguments
        var allArgs = new List<string>();
        foreach (var inj in ctor.InjectableParameters)
        {
            if (inj.IsKeyed)
            {
                allArgs.Add($"sp.GetRequiredKeyedService<{inj.TypeName}>(\"{GeneratorHelpers.EscapeStringLiteral(inj.ServiceKey!)}\")");
            }
            else
            {
                allArgs.Add($"sp.GetRequiredService<{inj.TypeName}>()");
            }
        }
        foreach (var rt in ctor.RuntimeParameters)
        {
            allArgs.Add(rt.ParameterName ?? GeneratorHelpers.ToCamelCase(GeneratorHelpers.GetSimpleTypeName(rt.TypeName)));
        }

        for (int i = 0; i < allArgs.Count; i++)
        {
            var arg = allArgs[i];
            var isLast = i == allArgs.Count - 1;
            builder.AppendLine($"{indent}        {arg}{(isLast ? ")" : ",")}");
        }
        builder.AppendLine($"{indent});");
    }
}

