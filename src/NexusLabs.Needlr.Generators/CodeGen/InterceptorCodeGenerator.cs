// Copyright (c) NexusLabs. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Linq;
using System.Text;

using NexusLabs.Needlr.Generators.Models;

namespace NexusLabs.Needlr.Generators.CodeGen;

/// <summary>
/// Generates interceptor proxy classes for AOP-style method interception.
/// </summary>
internal static class InterceptorCodeGenerator
{
    internal static void GenerateInterceptorProxyClass(StringBuilder builder, DiscoveredInterceptedService service, BreadcrumbWriter breadcrumbs, string? projectDirectory)
    {
        var proxyTypeName = GeneratorHelpers.GetProxyTypeName(service.TypeName);
        var shortTypeName = GeneratorHelpers.GetShortTypeName(service.TypeName);

        // Write verbose breadcrumb for interceptor proxy
        if (breadcrumbs.Level == BreadcrumbLevel.Verbose)
        {
            var sourcePath = service.SourceFilePath != null 
                ? BreadcrumbWriter.GetRelativeSourcePath(service.SourceFilePath, projectDirectory)
                : $"[{service.AssemblyName}]";
            
            var interceptorsList = service.AllInterceptorTypeNames
                .Select((t, i) => $"  {i + 1}. {t.Split('.').Last()}")
                .ToArray();
            
            var proxiedMethods = service.Methods
                .Where(m => m.InterceptorTypeNames.Length > 0)
                .Select(m => m.Name)
                .ToArray();
            var forwardedMethods = service.Methods
                .Where(m => m.InterceptorTypeNames.Length == 0)
                .Select(m => m.Name)
                .ToArray();

            var lines = new List<string>
            {
                $"Source: {sourcePath}",
                $"Target Interface: {string.Join(", ", service.InterfaceNames.Select(i => i.Split('.').Last()))}",
                "Interceptors (execution order):"
            };
            lines.AddRange(interceptorsList);
            lines.Add($"Methods proxied: {(proxiedMethods.Length > 0 ? string.Join(", ", proxiedMethods) : "none")}");
            lines.Add($"Methods forwarded: {(forwardedMethods.Length > 0 ? string.Join(", ", forwardedMethods) : "none")}");
            
            breadcrumbs.WriteVerboseBox(builder, "",
                $"Interceptor Proxy: {shortTypeName}",
                lines.ToArray());
        }

        builder.AppendLine("/// <summary>");
        builder.AppendLine($"/// Interceptor proxy for {service.TypeName}.");
        builder.AppendLine("/// Routes method calls through configured interceptors.");
        builder.AppendLine("/// </summary>");
        builder.AppendLine("[global::System.CodeDom.Compiler.GeneratedCodeAttribute(\"NexusLabs.Needlr.Generators\", \"1.0.0\")]");

        // Implement all interfaces
        builder.Append($"internal sealed class {proxyTypeName}");
        if (service.InterfaceNames.Length > 0)
        {
            builder.Append(" : ");
            builder.Append(string.Join(", ", service.InterfaceNames));
        }
        builder.AppendLine();
        builder.AppendLine("{");

        // Fields
        builder.AppendLine($"    private readonly {service.TypeName} _target;");
        builder.AppendLine("    private readonly IServiceProvider _serviceProvider;");
        builder.AppendLine();

        // Static MethodInfo fields for each method
        var methodIndex = 0;
        foreach (var method in service.Methods)
        {
            builder.AppendLine($"    internal static readonly MethodInfo _method{methodIndex} = typeof({method.InterfaceTypeName}).GetMethod(nameof({method.InterfaceTypeName}.{method.Name}))!;");
            methodIndex++;
        }
        builder.AppendLine();

        // Constructor
        builder.AppendLine($"    public {proxyTypeName}({service.TypeName} target, IServiceProvider serviceProvider)");
        builder.AppendLine("    {");
        builder.AppendLine("        _target = target ?? throw new ArgumentNullException(nameof(target));");
        builder.AppendLine("        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));");
        builder.AppendLine("    }");
        builder.AppendLine();

        // Generate each method
        methodIndex = 0;
        foreach (var method in service.Methods)
        {
            GenerateInterceptedMethod(builder, method, methodIndex, service.TypeName, breadcrumbs);
            methodIndex++;
        }

        builder.AppendLine("}");
    }

    internal static void GenerateInterceptedMethod(StringBuilder builder, TypeDiscoveryHelper.InterceptedMethodInfo method, int methodIndex, string targetTypeName, BreadcrumbWriter breadcrumbs)
    {
        var parameterList = method.GetParameterList();
        var argumentList = method.GetArgumentList();

        // Write breadcrumb for method
        if (method.InterceptorTypeNames.Length > 0)
        {
            var interceptorNames = string.Join(" → ", method.InterceptorTypeNames.Select(t => t.Split('.').Last()));
            breadcrumbs.WriteInlineComment(builder, "    ", $"{method.Name}: {interceptorNames}");
        }
        else
        {
            breadcrumbs.WriteInlineComment(builder, "    ", $"{method.Name}: direct forward (no interceptors)");
        }

        builder.AppendLine($"    public {method.ReturnType} {method.Name}({parameterList})");
        builder.AppendLine("    {");

        // If no interceptors, just forward directly to target
        if (method.InterceptorTypeNames.Length == 0)
        {
            if (method.IsVoid)
            {
                builder.AppendLine($"        _target.{method.Name}({argumentList});");
            }
            else
            {
                builder.AppendLine($"        return _target.{method.Name}({argumentList});");
            }
            builder.AppendLine("    }");
            builder.AppendLine();
            return;
        }

        // Build interceptor chain
        var interceptorCount = method.InterceptorTypeNames.Length;
        builder.AppendLine($"        var interceptors = new IMethodInterceptor[{interceptorCount}];");
        for (var i = 0; i < interceptorCount; i++)
        {
            builder.AppendLine($"        interceptors[{i}] = _serviceProvider.GetRequiredService<{method.InterceptorTypeNames[i]}>();");
        }
        builder.AppendLine();

        // Create arguments array
        if (method.Parameters.Count > 0)
        {
            builder.AppendLine($"        var args = new object?[] {{ {string.Join(", ", method.Parameters.Select(p => p.Name))} }};");
        }
        else
        {
            builder.AppendLine("        var args = Array.Empty<object?>();");
        }
        builder.AppendLine();

        // Build the proceed chain - start from innermost (actual call) and wrap outward
        builder.AppendLine("        // Build the interceptor chain from inside out");
        builder.AppendLine("        Func<ValueTask<object?>> proceed = async () =>");
        builder.AppendLine("        {");

        if (method.IsVoid)
        {
            builder.AppendLine($"            _target.{method.Name}({argumentList});");
            builder.AppendLine("            return null;");
        }
        else if (method.IsAsync)
        {
            // Check if the return type is Task<T> or ValueTask<T> (has a result)
            var hasResult = !method.ReturnType.Equals("global::System.Threading.Tasks.Task", StringComparison.Ordinal) &&
                           !method.ReturnType.Equals("global::System.Threading.Tasks.ValueTask", StringComparison.Ordinal);
            
            if (hasResult)
            {
                builder.AppendLine($"            var result = await _target.{method.Name}({argumentList});");
                builder.AppendLine("            return result;");
            }
            else
            {
                builder.AppendLine($"            await _target.{method.Name}({argumentList});");
                builder.AppendLine("            return null;");
            }
        }
        else
        {
            builder.AppendLine($"            var result = _target.{method.Name}({argumentList});");
            builder.AppendLine("            return result;");
        }

        builder.AppendLine("        };");
        builder.AppendLine();

        // Wrap each interceptor, from last to first (so first interceptor is outermost)
        builder.AppendLine("        for (var i = interceptors.Length - 1; i >= 0; i--)");
        builder.AppendLine("        {");
        builder.AppendLine("            var interceptor = interceptors[i];");
        builder.AppendLine("            var nextProceed = proceed;");
        builder.AppendLine($"            proceed = () => interceptor.InterceptAsync(new MethodInvocation(_target, _method{methodIndex}, args, nextProceed));");
        builder.AppendLine("        }");
        builder.AppendLine();

        // Invoke the chain and return the result
        if (method.IsVoid)
        {
            builder.AppendLine("        proceed().AsTask().GetAwaiter().GetResult();");
        }
        else if (method.IsAsync)
        {
            var hasResult = !method.ReturnType.Equals("global::System.Threading.Tasks.Task", StringComparison.Ordinal) &&
                           !method.ReturnType.Equals("global::System.Threading.Tasks.ValueTask", StringComparison.Ordinal);
            
            if (hasResult)
            {
                // Extract the inner type from Task<T> or ValueTask<T>
                var innerType = GeneratorHelpers.ExtractGenericTypeArgument(method.ReturnType);
                if (method.ReturnType.StartsWith("global::System.Threading.Tasks.ValueTask<", StringComparison.Ordinal))
                {
                    builder.AppendLine($"        return new {method.ReturnType}(proceed().AsTask().ContinueWith(t => ({innerType})t.Result!));");
                }
                else
                {
                    // Task<T>
                    builder.AppendLine($"        return proceed().AsTask().ContinueWith(t => ({innerType})t.Result!);");
                }
            }
            else
            {
                // Task or ValueTask without result
                if (method.ReturnType.StartsWith("global::System.Threading.Tasks.ValueTask", StringComparison.Ordinal))
                {
                    builder.AppendLine("        return new global::System.Threading.Tasks.ValueTask(proceed().AsTask());");
                }
                else
                {
                    builder.AppendLine("        return proceed().AsTask();");
                }
            }
        }
        else
        {
            // Synchronous with return value
            builder.AppendLine($"        return ({method.ReturnType})proceed().AsTask().GetAwaiter().GetResult()!;");
        }

        builder.AppendLine("    }");
        builder.AppendLine();
    }
}

