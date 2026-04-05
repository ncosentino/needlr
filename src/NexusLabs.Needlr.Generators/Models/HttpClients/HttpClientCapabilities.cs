using System;

namespace NexusLabs.Needlr.Generators.Models;

/// <summary>
/// Bit flags describing which HttpClient capability interfaces a discovered options type
/// implements. The generator emits wiring for each capability only when its bit is set.
/// </summary>
[Flags]
internal enum HttpClientCapabilities
{
    None = 0,
    Timeout = 1 << 0,
    UserAgent = 1 << 1,
    BaseAddress = 1 << 2,
    Headers = 1 << 3,
}
