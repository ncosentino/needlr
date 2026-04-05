// Copyright (c) NexusLabs. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Text;

using NexusLabs.Needlr.Generators.Models;

namespace NexusLabs.Needlr.Generators.CodeGen;

/// <summary>
/// Emits the body of the <c>RegisterHttpClients</c> method for discovered
/// <c>[HttpClientOptions]</c> types. Each discovered type produces:
/// <list type="number">
/// <item><description>An <c>AddOptions&lt;T&gt;().BindConfiguration(section)</c> call so <c>IOptions&lt;T&gt;</c> and friends resolve.</description></item>
/// <item><description>A <c>services.AddHttpClient(clientName, (sp, client) =&gt; { ... })</c> call whose body conditionally wires each capability implemented by the type.</description></item>
/// </list>
/// The conditional emission is the load-bearing design choice: future capabilities
/// (resilience, handler chain, handler lifetime, etc.) are added by introducing a
/// new capability flag + a new emission block here, with no change to the attribute
/// or existing consumers.
/// </summary>
internal static class HttpClientCodeGenerator
{
    /// <summary>
    /// Emits HttpClient registration statements into the supplied <see cref="StringBuilder"/>.
    /// The caller is responsible for opening/closing the containing method.
    /// </summary>
    /// <param name="builder">The builder to append to (already positioned inside a method body).</param>
    /// <param name="httpClients">The discovered HttpClient options types to emit registrations for.</param>
    public static void EmitHttpClientRegistrations(
        StringBuilder builder,
        IReadOnlyList<DiscoveredHttpClient> httpClients)
    {
        foreach (var http in httpClients)
        {
            builder.AppendLine();
            builder.AppendLine($"        // Named HttpClient: \"{http.ClientName}\" (bound to \"{http.SectionName}\")");

            // Step 1: IOptions<T> binding so consumers can inject IOptions<WebFetchHttpClientOptions>
            // if they need runtime access to the record alongside the HttpClient.
            builder.AppendLine($"        services.AddOptions<{http.TypeName}>().BindConfiguration(\"{http.SectionName}\");");

            // Step 2: the named AddHttpClient call with a capability-driven configuration callback.
            builder.AppendLine($"        services.AddHttpClient(\"{EscapeStringLiteral(http.ClientName)}\", (sp, client) =>");
            builder.AppendLine("        {");
            builder.AppendLine($"            var options = sp.GetRequiredService<global::Microsoft.Extensions.Options.IOptions<{http.TypeName}>>().Value;");

            if ((http.Capabilities & HttpClientCapabilities.Timeout) != 0)
            {
                builder.AppendLine("            client.Timeout = options.Timeout;");
            }

            if ((http.Capabilities & HttpClientCapabilities.BaseAddress) != 0)
            {
                builder.AppendLine("            if (options.BaseAddress is not null)");
                builder.AppendLine("            {");
                builder.AppendLine("                client.BaseAddress = options.BaseAddress;");
                builder.AppendLine("            }");
            }

            if ((http.Capabilities & HttpClientCapabilities.UserAgent) != 0)
            {
                builder.AppendLine("            if (!string.IsNullOrEmpty(options.UserAgent))");
                builder.AppendLine("            {");
                builder.AppendLine("                client.DefaultRequestHeaders.UserAgent.ParseAdd(options.UserAgent);");
                builder.AppendLine("            }");
            }

            if ((http.Capabilities & HttpClientCapabilities.Headers) != 0)
            {
                builder.AppendLine("            if (options.DefaultHeaders is not null)");
                builder.AppendLine("            {");
                builder.AppendLine("                foreach (var kvp in options.DefaultHeaders)");
                builder.AppendLine("                {");
                builder.AppendLine("                    client.DefaultRequestHeaders.Add(kvp.Key, kvp.Value);");
                builder.AppendLine("                }");
                builder.AppendLine("            }");
            }

            builder.AppendLine("        });");
        }
    }

    private static string EscapeStringLiteral(string value)
    {
        return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }
}
