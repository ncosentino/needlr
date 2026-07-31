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
    private static string GetParameterBaseName(TypeDiscoveryHelper.ConstructorParameterInfo parameter)
    {
        if (!string.IsNullOrWhiteSpace(parameter.ParameterName))
            return parameter.ParameterName!;

        var typeName = GeneratorHelpers.GetShortTypeName(parameter.TypeName);
        var genericStart = typeName.IndexOf('<');
        if (genericStart >= 0)
            typeName = typeName.Substring(0, genericStart);

        return GeneratorHelpers.ToCamelCase(typeName);
    }

    private static string GetParameterIdentifierName(TypeDiscoveryHelper.ConstructorParameterInfo parameter)
    {
        return GeneratorHelpers.EscapeIdentifier(GetParameterBaseName(parameter));
    }

    private static Dictionary<string, string> GetInjectableNamesByType(
        IEnumerable<TypeDiscoveryHelper.ConstructorParameterInfo> parameters)
    {
        var namesByType = new Dictionary<string, string>(StringComparer.Ordinal);
        var usedNames = new HashSet<string>(StringComparer.Ordinal);

        foreach (var parameter in parameters)
        {
            if (namesByType.ContainsKey(parameter.TypeName))
                continue;

            var baseName = GetParameterBaseName(parameter);
            var uniqueName = baseName;
            for (var suffix = 2; !usedNames.Add(uniqueName); suffix++)
                uniqueName = baseName + suffix;

            namesByType.Add(parameter.TypeName, uniqueName);
        }

        return namesByType;
    }

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
                $"{p.TypeName} {GetParameterIdentifierName(p)}"));

            builder.AppendLine($"    /// <summary>Creates a new instance of {factory.SimpleTypeName}.</summary>");
            
            // Add <param> tags for documented runtime parameters
            foreach (var param in ctor.RuntimeParameters)
            {
                if (!string.IsNullOrWhiteSpace(param.DocumentationComment))
                {
                    var paramName = GetParameterBaseName(param);
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

        var allInjectableParams = factory.Constructors
            .SelectMany(c => c.InjectableParameters)
            .GroupBy(p => p.TypeName)
            .Select(g => g.First())
            .ToList();
        var injectableNamesByType = GetInjectableNamesByType(allInjectableParams);

        builder.AppendLine("/// <summary>");
        builder.AppendLine($"/// Factory implementation for creating instances of <see cref=\"{factory.TypeName}\"/>.");
        builder.AppendLine("/// </summary>");
        builder.AppendLine("[global::System.CodeDom.Compiler.GeneratedCodeAttribute(\"NexusLabs.Needlr.Generators\", \"1.0.0\")]");
        builder.AppendLine($"internal sealed class {factoryImplName} : {factoryInterfaceName}");
        builder.AppendLine("{");

        // Fields for injectable dependencies
        foreach (var param in allInjectableParams)
        {
            var fieldName = "_" + injectableNamesByType[param.TypeName];
            builder.AppendLine($"    private readonly {param.TypeName} {fieldName};");
        }

        builder.AppendLine();

        // Constructor
        var ctorParams = string.Join(
            ", ",
            allInjectableParams.Select(p =>
                $"{p.TypeName} {GeneratorHelpers.EscapeIdentifier(injectableNamesByType[p.TypeName])}"));
        builder.AppendLine($"    public {factoryImplName}({ctorParams})");
        builder.AppendLine("    {");
        foreach (var param in allInjectableParams)
        {
            var parameterName = injectableNamesByType[param.TypeName];
            var fieldName = "_" + parameterName;
            var paramName = GeneratorHelpers.EscapeIdentifier(parameterName);
            builder.AppendLine($"        {fieldName} = {paramName};");
        }
        builder.AppendLine("    }");
        builder.AppendLine();

        // Create methods for each constructor
        foreach (var ctor in factory.Constructors)
        {
            var runtimeParamList = string.Join(", ", ctor.RuntimeParameters.Select(p =>
                $"{p.TypeName} {GetParameterIdentifierName(p)}"));

            builder.AppendLine($"    public {factory.ReturnTypeName} Create({runtimeParamList})");
            builder.AppendLine("    {");
            builder.Append($"        return new {factory.TypeName}(");

            // Build constructor arguments as named arguments (paramName: value) rather
            // than positionally. A generated constructor's parameter order follows field
            // declaration order and need not match this injectable-then-runtime grouping,
            // so binding by name is required for correctness regardless of which order
            // the target constructor was declared or generated in.
            var allArgs = new List<string>();
            foreach (var inj in ctor.InjectableParameters)
            {
                var fieldName = "_" + injectableNamesByType[inj.TypeName];
                var argName = GetParameterIdentifierName(inj);
                allArgs.Add($"{argName}: {fieldName}");
            }
            foreach (var rt in ctor.RuntimeParameters)
            {
                var paramName = GetParameterIdentifierName(rt);
                allArgs.Add($"{paramName}: {paramName}");
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
        var runtimeParams = string.Join(", ", ctor.RuntimeParameters.Select(GetParameterIdentifierName));

        builder.AppendLine($"{indent}services.AddSingleton<{funcType}>(sp =>");
        builder.AppendLine($"{indent}    ({runtimeParams}) => new {factory.TypeName}(");

        // Build constructor call arguments as named arguments — see the matching
        // comment in GenerateFactoryImplementation for why positional binding is unsafe
        // here.
        var allArgs = new List<string>();
        foreach (var inj in ctor.InjectableParameters)
        {
            var argName = GetParameterIdentifierName(inj);
            if (inj.IsKeyed)
            {
                allArgs.Add($"{argName}: sp.GetRequiredKeyedService<{inj.TypeName}>(\"{GeneratorHelpers.EscapeStringLiteral(inj.ServiceKey!)}\")");
            }
            else
            {
                allArgs.Add($"{argName}: sp.GetRequiredService<{inj.TypeName}>()");
            }
        }
        foreach (var rt in ctor.RuntimeParameters)
        {
            var paramName = GetParameterIdentifierName(rt);
            allArgs.Add($"{paramName}: {paramName}");
        }

        for (int i = 0; i < allArgs.Count; i++)
        {
            var arg = allArgs[i];
            var isLast = i == allArgs.Count - 1;
            builder.AppendLine($"{indent}        {arg}{(isLast ? ")" : ",")}");
        }
        builder.AppendLine($"{indent});");
    }

    /// <summary>
    /// Generates the complete Factories.g.cs source file containing factory
    /// interfaces, implementations, and the FactoryRegistrations helper.
    /// </summary>
    internal static string GenerateFactoriesSource(IReadOnlyList<DiscoveredFactory> factories, string assemblyName, BreadcrumbWriter breadcrumbs, string? projectDirectory)
    {
        var builder = new StringBuilder();
        var safeAssemblyName = GeneratorHelpers.SanitizeIdentifier(assemblyName);

        breadcrumbs.WriteFileHeader(builder, assemblyName, "Needlr Generated Factories");
        builder.AppendLine("#nullable enable");
        builder.AppendLine();
        builder.AppendLine("using System;");
        builder.AppendLine();
        builder.AppendLine("using Microsoft.Extensions.DependencyInjection;");
        builder.AppendLine();
        builder.AppendLine($"namespace {safeAssemblyName}.Generated;");
        builder.AppendLine();

        // Generate factory interfaces and implementations for each type
        foreach (var factory in factories)
        {
            if (factory.GenerateInterface)
            {
                GenerateFactoryInterface(builder, factory, breadcrumbs, projectDirectory);
                builder.AppendLine();
                GenerateFactoryImplementation(builder, factory, breadcrumbs, projectDirectory);
                builder.AppendLine();
            }
        }

        // Generate the registration helper
        builder.AppendLine("/// <summary>");
        builder.AppendLine("/// Helper class for registering factory types.");
        builder.AppendLine("/// </summary>");
        builder.AppendLine("[global::System.CodeDom.Compiler.GeneratedCodeAttribute(\"NexusLabs.Needlr.Generators\", \"1.0.0\")]");
        builder.AppendLine("public static class FactoryRegistrations");
        builder.AppendLine("{");
        builder.AppendLine("    /// <summary>");
        builder.AppendLine("    /// Registers all generated factories.");
        builder.AppendLine("    /// </summary>");
        builder.AppendLine("    /// <param name=\"services\">The service collection to register to.</param>");
        builder.AppendLine("    public static void RegisterFactories(IServiceCollection services)");
        builder.AppendLine("    {");

        foreach (var factory in factories)
        {
            breadcrumbs.WriteInlineComment(builder, "        ", $"Factory for {factory.SimpleTypeName}");

            // Register Func<> for each constructor
            if (factory.GenerateFunc)
            {
                foreach (var ctor in factory.Constructors)
                {
                    GenerateFuncRegistration(builder, factory, ctor, "        ");
                }
            }

            // Register interface factory
            if (factory.GenerateInterface)
            {
                var factoryInterfaceName = $"I{factory.SimpleTypeName}Factory";
                var factoryImplName = $"{factory.SimpleTypeName}Factory";
                builder.AppendLine($"        services.AddSingleton<global::{safeAssemblyName}.Generated.{factoryInterfaceName}, global::{safeAssemblyName}.Generated.{factoryImplName}>();");
            }
        }

        builder.AppendLine("    }");
        builder.AppendLine();
        builder.AppendLine("    /// <summary>");
        builder.AppendLine("    /// Gets the number of factory types generated at compile time.");
        builder.AppendLine("    /// </summary>");
        builder.AppendLine($"    public static int Count => {factories.Count};");
        builder.AppendLine("}");

        return builder.ToString();
    }
}
