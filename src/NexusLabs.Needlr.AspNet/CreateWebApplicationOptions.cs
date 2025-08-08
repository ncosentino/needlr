using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace NexusLabs.Needlr.AspNet;

public sealed record CreateWebApplicationOptions(
    WebApplicationOptions Options,
    ILogger Logger)
{
    public CreateWebApplicationOptions(
        WebApplicationOptions options)
        : this(options, NullLogger.Instance)
    {
        ArgumentNullException.ThrowIfNull(options);
    }
}
