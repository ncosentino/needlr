using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using System.Reflection;

namespace NexusLabs.Needlr.AspNet;

public sealed record CreateWebApplicationOptions(
    WebApplicationOptions Options,
    IReadOnlyList<Assembly> AssembliesToLoadFrom,
    ILogger Logger)
{
    public CreateWebApplicationOptions(
        WebApplicationOptions options,
        IReadOnlyList<Assembly> assembliesToLoadFrom)
        : this(options, assembliesToLoadFrom, NullLogger.Instance)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(assembliesToLoadFrom);
    }
}
