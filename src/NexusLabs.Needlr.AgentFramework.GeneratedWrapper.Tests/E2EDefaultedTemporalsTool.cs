using System.ComponentModel;

namespace NexusLabs.Needlr.AgentFramework.GeneratedWrapper.Tests;

[AgentFunctionGroup("e2e-defaulted-temporals")]
public sealed class E2EDefaultedTemporalsTool
{
    public static Guid? CapturedGuid { get; set; }
    public static DateTime? CapturedDateTime { get; set; }
    public static DateTimeOffset? CapturedDateTimeOffset { get; set; }
    public static TimeSpan? CapturedTimeSpan { get; set; }
    public static decimal? CapturedDecimal { get; set; }

    [AgentFunction]
    [Description("Records an optional Guid (defaults to Guid.Empty via default literal).")]
    public string RecordGuid(
        [Description("Guid value.")] Guid id = default)
    {
        CapturedGuid = id;
        return "ok";
    }

    [AgentFunction]
    [Description("Records an optional DateTime (defaults to DateTime.MinValue via default literal).")]
    public string RecordDateTime(
        [Description("DateTime value.")] DateTime when = default)
    {
        CapturedDateTime = when;
        return "ok";
    }

    [AgentFunction]
    [Description("Records an optional DateTimeOffset (defaults to MinValue via default literal).")]
    public string RecordDateTimeOffset(
        [Description("DateTimeOffset value.")] DateTimeOffset stamp = default)
    {
        CapturedDateTimeOffset = stamp;
        return "ok";
    }

    [AgentFunction]
    [Description("Records an optional TimeSpan (defaults to TimeSpan.Zero via default literal).")]
    public string RecordTimeSpan(
        [Description("TimeSpan value.")] TimeSpan duration = default)
    {
        CapturedTimeSpan = duration;
        return "ok";
    }

    [AgentFunction]
    [Description("Records an optional decimal with a non-default literal.")]
    public string RecordDecimal(
        [Description("Decimal value.")] decimal price = 9.99m)
    {
        CapturedDecimal = price;
        return "ok";
    }
}
