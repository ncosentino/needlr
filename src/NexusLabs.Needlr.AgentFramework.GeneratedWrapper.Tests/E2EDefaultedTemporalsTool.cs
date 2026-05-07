using System.ComponentModel;

namespace NexusLabs.Needlr.AgentFramework.GeneratedWrapper.Tests;

[AgentFunctionGroup("e2e-defaulted-temporals")]
public sealed class E2EDefaultedTemporalsTool
{
    public sealed class Capture
    {
        public Guid? Guid { get; set; }
        public DateTime? DateTime { get; set; }
        public DateTimeOffset? DateTimeOffset { get; set; }
        public TimeSpan? TimeSpan { get; set; }
        public decimal? Decimal { get; set; }
    }

    private readonly Capture _capture;

    public E2EDefaultedTemporalsTool(Capture capture)
    {
        _capture = capture;
    }

    [AgentFunction]
    [Description("Records an optional Guid (defaults to Guid.Empty via default literal).")]
    public string RecordGuid(
        [Description("Guid value.")] Guid id = default)
    {
        _capture.Guid = id;
        return "ok";
    }

    [AgentFunction]
    [Description("Records an optional DateTime (defaults to DateTime.MinValue via default literal).")]
    public string RecordDateTime(
        [Description("DateTime value.")] DateTime when = default)
    {
        _capture.DateTime = when;
        return "ok";
    }

    [AgentFunction]
    [Description("Records an optional DateTimeOffset (defaults to MinValue via default literal).")]
    public string RecordDateTimeOffset(
        [Description("DateTimeOffset value.")] DateTimeOffset stamp = default)
    {
        _capture.DateTimeOffset = stamp;
        return "ok";
    }

    [AgentFunction]
    [Description("Records an optional TimeSpan (defaults to TimeSpan.Zero via default literal).")]
    public string RecordTimeSpan(
        [Description("TimeSpan value.")] TimeSpan duration = default)
    {
        _capture.TimeSpan = duration;
        return "ok";
    }

    [AgentFunction]
    [Description("Records an optional decimal with a non-default literal.")]
    public string RecordDecimal(
        [Description("Decimal value.")] decimal price = 9.99m)
    {
        _capture.Decimal = price;
        return "ok";
    }
}
