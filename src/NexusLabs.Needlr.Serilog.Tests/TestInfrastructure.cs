using Serilog.Core;
using Serilog.Events;

namespace NexusLabs.Needlr.Serilog.Tests;

/// <summary>
/// Marker type so the Needlr source generator emits a TypeRegistry and
/// [ModuleInitializer] for this test assembly, enabling source-gen plugin
/// discovery of SerilogPlugin.
/// </summary>
internal sealed class TestAssemblyMarker;

/// <summary>
/// In-memory Serilog sink for capturing log events in tests.
/// </summary>
internal sealed class CapturingSink : ILogEventSink
{
    public readonly List<LogEvent> Events = [];

    public void Emit(LogEvent logEvent) => Events.Add(logEvent);
}
