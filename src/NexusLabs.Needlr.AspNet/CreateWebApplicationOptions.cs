using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace NexusLabs.Needlr.AspNet;

public sealed record CreateWebApplicationOptions(
    WebApplicationOptions Options,
    ILogger Logger)
{
    private static readonly CreateWebApplicationOptions _defaultOptions = new(options: new());

    public CreateWebApplicationOptions(
        WebApplicationOptions options)
        : this(options, NullLogger.Instance)
    {
        ArgumentNullException.ThrowIfNull(options);
    }

    public static CreateWebApplicationOptions Default => _defaultOptions;
}
