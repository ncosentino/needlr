using System.Globalization;
using System.Text.Json;

namespace NexusLabs.Needlr.AgentFramework;

/// <summary>
/// Kind-tolerant argument extractors for source-generated <c>[AgentFunction]</c> wrappers.
/// </summary>
/// <remarks>
/// <para>
/// <c>Microsoft.Extensions.AI.AIFunctionArguments</c> delivers tool argument values as
/// <see langword="object"/> — but the underlying <c>IChatClient</c> determines the
/// runtime shape:
/// </para>
/// <list type="bullet">
/// <item>
/// <description>
/// GitHub Copilot's <c>IChatClient</c> stringifies values: arrays/objects arrive as
/// <see cref="JsonElement"/> with <see cref="JsonValueKind.String"/>.
/// </description>
/// </item>
/// <item>
/// <description>
/// <c>AzureOpenAIClient.AsIChatClient()</c> (and most others) parse the model's tool-call
/// JSON literally: arrays arrive as <see cref="JsonValueKind.Array"/>, objects as
/// <see cref="JsonValueKind.Object"/>, numbers as <see cref="JsonValueKind.Number"/>, etc.
/// </description>
/// </item>
/// </list>
/// <para>
/// Calling <c>JsonElement.GetString()</c> / <c>GetInt32()</c> / <c>GetBoolean()</c>
/// directly throws <see cref="InvalidOperationException"/> when <see cref="JsonValueKind"/>
/// doesn't match. These extractors translate per-kind so the source generator can emit a
/// single uniform call regardless of the chat client's delivery shape.
/// </para>
/// <para>
/// <strong>Extractors assume the value is present and non-null.</strong> The generator is
/// responsible for missing-key, <see cref="JsonValueKind.Null"/>, and
/// <see cref="JsonValueKind.Undefined"/> handling — typically by short-circuiting to a
/// declared default value or to <see langword="null"/> for nullable parameter types.
/// <see cref="GetStringArgument(object?)"/> is the exception: it returns
/// <see cref="string.Empty"/> for null/undefined inputs to preserve compatibility with the
/// generator's existing emission shape.
/// </para>
/// <para>
/// <strong>Strict semantics for typed primitives.</strong> Booleans receiving numeric kinds
/// throw rather than coerce <c>0</c>/<c>1</c> — the model is violating its own schema; the
/// helper does not paper over it. Numeric extractors prefer the precision-preserving
/// <c>TryGet*</c> overload (<c>TryGetDecimal</c> for decimal, <c>TryGetDouble</c> for double)
/// and fall back to invariant-culture parsing on <see cref="JsonValueKind.String"/>.
/// </para>
/// <para>
/// <strong>Supported parameter types.</strong> <see cref="string"/>, <see cref="bool"/>, the
/// 8 integer types (<see cref="byte"/>/<see cref="sbyte"/>/<see cref="short"/>/<see cref="ushort"/>/
/// <see cref="int"/>/<see cref="uint"/>/<see cref="long"/>/<see cref="ulong"/>),
/// <see cref="float"/>/<see cref="double"/>/<see cref="decimal"/>, <see cref="Guid"/>,
/// <see cref="DateTime"/>, <see cref="DateTimeOffset"/>, and <see cref="TimeSpan"/>. JSON has
/// no native kind for the temporal/GUID types — the model emits a string and the extractor
/// parses it (ISO 8601 for date-time / duration, GUID literal for <see cref="Guid"/>).
/// </para>
/// </remarks>
public static class AgentFrameworkArgumentExtractor
{
    /// <summary>
    /// Extracts a <see cref="string"/> argument from a raw <see cref="object"/> delivered
    /// by <c>AIFunctionArguments</c>.
    /// </summary>
    /// <param name="raw">The raw argument value.</param>
    /// <returns>
    /// <see cref="JsonElement"/> with <see cref="JsonValueKind.String"/> →
    /// <see cref="JsonElement.GetString"/> (or empty string if null);<br/>
    /// <see cref="JsonValueKind.Null"/> or <see cref="JsonValueKind.Undefined"/> →
    /// <see cref="string.Empty"/>;<br/>
    /// <see cref="JsonValueKind.Array"/>, <see cref="JsonValueKind.Object"/>,
    /// <see cref="JsonValueKind.Number"/>, <see cref="JsonValueKind.True"/>,
    /// <see cref="JsonValueKind.False"/> → <see cref="JsonElement.GetRawText"/>;<br/>
    /// already-typed <see cref="string"/> → as-is;<br/>
    /// <see langword="null"/> → <see cref="string.Empty"/>;<br/>
    /// any other object → <see cref="object.ToString"/> (or empty string).
    /// </returns>
    public static string GetStringArgument(object? raw)
    {
        if (raw is null)
        {
            return string.Empty;
        }

        if (raw is JsonElement je)
        {
            return je.ValueKind switch
            {
                JsonValueKind.String => je.GetString() ?? string.Empty,
                JsonValueKind.Null or JsonValueKind.Undefined => string.Empty,
                _ => je.GetRawText(),
            };
        }

        if (raw is string s)
        {
            return s;
        }

        return raw.ToString() ?? string.Empty;
    }

    /// <summary>
    /// Extracts a <see cref="bool"/> argument with strict kind semantics.
    /// </summary>
    /// <param name="raw">The raw argument value.</param>
    /// <returns>
    /// <see cref="JsonValueKind.True"/>/<see cref="JsonValueKind.False"/> →
    /// <see cref="JsonElement.GetBoolean"/>;<br/>
    /// <see cref="JsonValueKind.String"/> → <see cref="bool.TryParse(string?, out bool)"/>;<br/>
    /// already-typed <see cref="bool"/> → as-is.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown for <see cref="JsonValueKind.Number"/>, <see cref="JsonValueKind.Array"/>,
    /// <see cref="JsonValueKind.Object"/>, <see cref="JsonValueKind.Null"/>,
    /// <see cref="JsonValueKind.Undefined"/>, or unparseable strings such as <c>"1"</c> or
    /// <c>"yes"</c>. This is intentional — silently coercing <c>0</c>/<c>1</c> or
    /// <c>"yes"</c>/<c>"no"</c> would mask schema violations from the model.
    /// </exception>
    public static bool GetBooleanArgument(object? raw)
    {
        if (raw is bool b)
        {
            return b;
        }

        if (raw is JsonElement je)
        {
            switch (je.ValueKind)
            {
                case JsonValueKind.True:
                case JsonValueKind.False:
                    return je.GetBoolean();
                case JsonValueKind.String:
                    var s = je.GetString();
                    if (bool.TryParse(s, out var parsed))
                    {
                        return parsed;
                    }
                    throw new InvalidOperationException(
                        $"Cannot extract bool argument: JSON String '{s}' is not 'true' or 'false'.");
                default:
                    throw new InvalidOperationException(
                        $"Cannot extract bool argument: unexpected JsonValueKind {je.ValueKind}.");
            }
        }

        throw new InvalidOperationException(
            $"Cannot extract bool argument from {raw?.GetType().FullName ?? "null"}.");
    }

    /// <summary>Extracts an <see cref="int"/> argument.</summary>
    /// <param name="raw">The raw argument value.</param>
    public static int GetInt32Argument(object? raw)
        => ExtractInteger<int>(
            raw,
            tryNumber: (JsonElement je, out int v) => je.TryGetInt32(out v),
            tryParseInvariant: s => int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : (int?)null);

    /// <summary>Extracts a <see cref="long"/> argument.</summary>
    /// <param name="raw">The raw argument value.</param>
    public static long GetInt64Argument(object? raw)
        => ExtractInteger<long>(
            raw,
            tryNumber: (JsonElement je, out long v) => je.TryGetInt64(out v),
            tryParseInvariant: s => long.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : (long?)null);

    /// <summary>Extracts a <see cref="short"/> argument.</summary>
    /// <param name="raw">The raw argument value.</param>
    public static short GetInt16Argument(object? raw)
        => ExtractInteger<short>(
            raw,
            tryNumber: (JsonElement je, out short v) => je.TryGetInt16(out v),
            tryParseInvariant: s => short.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : (short?)null);

    /// <summary>Extracts an <see cref="sbyte"/> argument.</summary>
    /// <param name="raw">The raw argument value.</param>
    public static sbyte GetSByteArgument(object? raw)
        => ExtractInteger<sbyte>(
            raw,
            tryNumber: (JsonElement je, out sbyte v) => je.TryGetSByte(out v),
            tryParseInvariant: s => sbyte.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : (sbyte?)null);

    /// <summary>Extracts a <see cref="byte"/> argument.</summary>
    /// <param name="raw">The raw argument value.</param>
    public static byte GetByteArgument(object? raw)
        => ExtractInteger<byte>(
            raw,
            tryNumber: (JsonElement je, out byte v) => je.TryGetByte(out v),
            tryParseInvariant: s => byte.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : (byte?)null);

    /// <summary>Extracts a <see cref="ushort"/> argument.</summary>
    /// <param name="raw">The raw argument value.</param>
    public static ushort GetUInt16Argument(object? raw)
        => ExtractInteger<ushort>(
            raw,
            tryNumber: (JsonElement je, out ushort v) => je.TryGetUInt16(out v),
            tryParseInvariant: s => ushort.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : (ushort?)null);

    /// <summary>Extracts a <see cref="uint"/> argument.</summary>
    /// <param name="raw">The raw argument value.</param>
    public static uint GetUInt32Argument(object? raw)
        => ExtractInteger<uint>(
            raw,
            tryNumber: (JsonElement je, out uint v) => je.TryGetUInt32(out v),
            tryParseInvariant: s => uint.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : (uint?)null);

    /// <summary>Extracts a <see cref="ulong"/> argument.</summary>
    /// <param name="raw">The raw argument value.</param>
    public static ulong GetUInt64Argument(object? raw)
        => ExtractInteger<ulong>(
            raw,
            tryNumber: (JsonElement je, out ulong v) => je.TryGetUInt64(out v),
            tryParseInvariant: s => ulong.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : (ulong?)null);

    /// <summary>Extracts a <see cref="float"/> argument.</summary>
    /// <param name="raw">The raw argument value.</param>
    /// <remarks>
    /// Rejects non-finite values (<see cref="float.NaN"/>, <see cref="float.PositiveInfinity"/>,
    /// <see cref="float.NegativeInfinity"/>) — JSON itself does not represent them, so receiving
    /// one indicates schema violation.
    /// </remarks>
    public static float GetSingleArgument(object? raw)
        => ExtractFloat<float>(
            raw,
            tryNumber: (JsonElement je, out float v) => je.TryGetSingle(out v),
            tryParseInvariant: s => float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : (float?)null,
            isFinite: float.IsFinite);

    /// <summary>Extracts a <see cref="double"/> argument with precision-first decoding.</summary>
    /// <param name="raw">The raw argument value.</param>
    /// <remarks>
    /// Uses <see cref="JsonElement.TryGetDouble"/> on <see cref="JsonValueKind.Number"/> for
    /// IEEE-754 fidelity. Rejects non-finite values per JSON spec.
    /// </remarks>
    public static double GetDoubleArgument(object? raw)
        => ExtractFloat<double>(
            raw,
            tryNumber: (JsonElement je, out double v) => je.TryGetDouble(out v),
            tryParseInvariant: s => double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : (double?)null,
            isFinite: double.IsFinite);

    /// <summary>Extracts a <see cref="decimal"/> argument with precision-preserving decoding.</summary>
    /// <param name="raw">The raw argument value.</param>
    /// <remarks>
    /// Uses <see cref="JsonElement.TryGetDecimal"/> on <see cref="JsonValueKind.Number"/> first
    /// to preserve precision that would be lost via a <see cref="double"/> round-trip. Falls
    /// back to invariant-culture parsing for <see cref="JsonValueKind.String"/>.
    /// </remarks>
    public static decimal GetDecimalArgument(object? raw)
    {
        if (raw is decimal d)
        {
            return d;
        }

        if (raw is JsonElement je)
        {
            switch (je.ValueKind)
            {
                case JsonValueKind.Number:
                    if (je.TryGetDecimal(out var v))
                    {
                        return v;
                    }
                    throw new InvalidOperationException(
                        $"Cannot extract decimal argument: JSON Number '{je.GetRawText()}' is not representable as decimal.");
                case JsonValueKind.String:
                    var s = je.GetString();
                    if (decimal.TryParse(s, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed))
                    {
                        return parsed;
                    }
                    throw new InvalidOperationException(
                        $"Cannot extract decimal argument: JSON String '{s}' is not a numeric literal.");
                default:
                    throw new InvalidOperationException(
                        $"Cannot extract decimal argument: unexpected JsonValueKind {je.ValueKind}.");
            }
        }

        throw new InvalidOperationException(
            $"Cannot extract decimal argument from {raw?.GetType().FullName ?? "null"}.");
    }

    /// <summary>Extracts a <see cref="Guid"/> argument.</summary>
    /// <param name="raw">The raw argument value.</param>
    /// <remarks>
    /// JSON has no native GUID kind — the model emits a string. Uses
    /// <see cref="JsonElement.TryGetGuid"/> on <see cref="JsonValueKind.String"/>; throws on
    /// any other kind to surface schema violations rather than silently substituting
    /// <see cref="Guid.Empty"/>.
    /// </remarks>
    public static Guid GetGuidArgument(object? raw)
    {
        if (raw is Guid g)
        {
            return g;
        }

        if (raw is JsonElement je)
        {
            switch (je.ValueKind)
            {
                case JsonValueKind.String:
                    if (je.TryGetGuid(out var v))
                    {
                        return v;
                    }
                    throw new InvalidOperationException(
                        $"Cannot extract Guid argument: JSON String '{je.GetString()}' is not a valid GUID.");
                default:
                    throw new InvalidOperationException(
                        $"Cannot extract Guid argument: unexpected JsonValueKind {je.ValueKind}.");
            }
        }

        throw new InvalidOperationException(
            $"Cannot extract Guid argument from {raw?.GetType().FullName ?? "null"}.");
    }

    /// <summary>Extracts a <see cref="DateTime"/> argument from an ISO 8601 string.</summary>
    /// <param name="raw">The raw argument value.</param>
    /// <remarks>
    /// JSON has no native date-time kind — the model emits a string. Uses
    /// <see cref="JsonElement.TryGetDateTime"/> which accepts the JSON-Schema-spec
    /// <c>"date-time"</c> format (ISO 8601 with optional offset).
    /// </remarks>
    public static DateTime GetDateTimeArgument(object? raw)
    {
        if (raw is DateTime dt)
        {
            return dt;
        }

        if (raw is JsonElement je)
        {
            switch (je.ValueKind)
            {
                case JsonValueKind.String:
                    if (je.TryGetDateTime(out var v))
                    {
                        return v;
                    }
                    throw new InvalidOperationException(
                        $"Cannot extract DateTime argument: JSON String '{je.GetString()}' is not a valid ISO 8601 date-time.");
                default:
                    throw new InvalidOperationException(
                        $"Cannot extract DateTime argument: unexpected JsonValueKind {je.ValueKind}.");
            }
        }

        throw new InvalidOperationException(
            $"Cannot extract DateTime argument from {raw?.GetType().FullName ?? "null"}.");
    }

    /// <summary>Extracts a <see cref="DateTimeOffset"/> argument from an ISO 8601 string.</summary>
    /// <param name="raw">The raw argument value.</param>
    /// <remarks>
    /// JSON has no native date-time kind — the model emits a string with optional offset.
    /// Uses <see cref="JsonElement.TryGetDateTimeOffset"/>.
    /// </remarks>
    public static DateTimeOffset GetDateTimeOffsetArgument(object? raw)
    {
        if (raw is DateTimeOffset dto)
        {
            return dto;
        }

        if (raw is JsonElement je)
        {
            switch (je.ValueKind)
            {
                case JsonValueKind.String:
                    if (je.TryGetDateTimeOffset(out var v))
                    {
                        return v;
                    }
                    throw new InvalidOperationException(
                        $"Cannot extract DateTimeOffset argument: JSON String '{je.GetString()}' is not a valid ISO 8601 date-time.");
                default:
                    throw new InvalidOperationException(
                        $"Cannot extract DateTimeOffset argument: unexpected JsonValueKind {je.ValueKind}.");
            }
        }

        throw new InvalidOperationException(
            $"Cannot extract DateTimeOffset argument from {raw?.GetType().FullName ?? "null"}.");
    }

    /// <summary>Extracts a <see cref="TimeSpan"/> argument from a string.</summary>
    /// <param name="raw">The raw argument value.</param>
    /// <remarks>
    /// <para>
    /// JSON has no native duration kind. This helper accepts both common LLM outputs:
    /// </para>
    /// <list type="bullet">
    /// <item>
    /// <description>
    /// .NET round-trip format like <c>"01:30:00"</c> or <c>"1.02:03:04"</c> via
    /// <see cref="TimeSpan.TryParse(string?, IFormatProvider?, out TimeSpan)"/> with invariant culture.
    /// </description>
    /// </item>
    /// <item>
    /// <description>
    /// JSON-Schema-spec ISO 8601 duration like <c>"PT1H30M"</c> via
    /// <see cref="System.Xml.XmlConvert.ToTimeSpan"/>.
    /// </description>
    /// </item>
    /// </list>
    /// </remarks>
    public static TimeSpan GetTimeSpanArgument(object? raw)
    {
        if (raw is TimeSpan ts)
        {
            return ts;
        }

        if (raw is JsonElement je)
        {
            switch (je.ValueKind)
            {
                case JsonValueKind.String:
                    var s = je.GetString();
                    if (TryParseTimeSpan(s, out var v))
                    {
                        return v;
                    }
                    throw new InvalidOperationException(
                        $"Cannot extract TimeSpan argument: JSON String '{s}' is neither a .NET duration ('hh:mm:ss') nor an ISO 8601 duration ('PT1H30M').");
                default:
                    throw new InvalidOperationException(
                        $"Cannot extract TimeSpan argument: unexpected JsonValueKind {je.ValueKind}.");
            }
        }

        throw new InvalidOperationException(
            $"Cannot extract TimeSpan argument from {raw?.GetType().FullName ?? "null"}.");
    }

    private static bool TryParseTimeSpan(string? s, out TimeSpan result)
    {
        if (string.IsNullOrWhiteSpace(s))
        {
            result = default;
            return false;
        }

        if (TimeSpan.TryParse(s, CultureInfo.InvariantCulture, out result))
        {
            return true;
        }

        try
        {
            result = System.Xml.XmlConvert.ToTimeSpan(s!);
            return true;
        }
        catch (FormatException)
        {
            result = default;
            return false;
        }
    }

    private delegate bool TryNumber<T>(JsonElement je, out T value);

    private static T ExtractInteger<T>(
        object? raw,
        TryNumber<T> tryNumber,
        Func<string?, T?> tryParseInvariant)
        where T : struct
    {
        if (raw is T typed)
        {
            return typed;
        }

        if (raw is JsonElement je)
        {
            switch (je.ValueKind)
            {
                case JsonValueKind.Number:
                    if (tryNumber(je, out var n))
                    {
                        return n;
                    }
                    throw new InvalidOperationException(
                        $"Cannot extract {typeof(T).Name} argument: JSON Number '{je.GetRawText()}' " +
                        $"is out of range or has a fractional component.");
                case JsonValueKind.String:
                    var s = je.GetString();
                    var parsed = tryParseInvariant(s);
                    if (parsed.HasValue)
                    {
                        return parsed.Value;
                    }
                    throw new InvalidOperationException(
                        $"Cannot extract {typeof(T).Name} argument: JSON String '{s}' is not a numeric literal.");
                default:
                    throw new InvalidOperationException(
                        $"Cannot extract {typeof(T).Name} argument: unexpected JsonValueKind {je.ValueKind}.");
            }
        }

        throw new InvalidOperationException(
            $"Cannot extract {typeof(T).Name} argument from {raw?.GetType().FullName ?? "null"}.");
    }

    private static T ExtractFloat<T>(
        object? raw,
        TryNumber<T> tryNumber,
        Func<string?, T?> tryParseInvariant,
        Func<T, bool> isFinite)
        where T : struct
    {
        if (raw is T typed)
        {
            if (!isFinite(typed))
            {
                throw new InvalidOperationException(
                    $"Cannot extract {typeof(T).Name} argument: value is not finite (NaN/Infinity).");
            }
            return typed;
        }

        if (raw is JsonElement je)
        {
            switch (je.ValueKind)
            {
                case JsonValueKind.Number:
                    if (tryNumber(je, out var n))
                    {
                        if (!isFinite(n))
                        {
                            throw new InvalidOperationException(
                                $"Cannot extract {typeof(T).Name} argument: " +
                                "JSON Number is not finite (NaN/Infinity).");
                        }
                        return n;
                    }
                    throw new InvalidOperationException(
                        $"Cannot extract {typeof(T).Name} argument: JSON Number '{je.GetRawText()}' " +
                        $"is not representable as {typeof(T).Name}.");
                case JsonValueKind.String:
                    var s = je.GetString();
                    var parsed = tryParseInvariant(s);
                    if (parsed.HasValue)
                    {
                        if (!isFinite(parsed.Value))
                        {
                            throw new InvalidOperationException(
                                $"Cannot extract {typeof(T).Name} argument: " +
                                "parsed value is not finite (NaN/Infinity).");
                        }
                        return parsed.Value;
                    }
                    throw new InvalidOperationException(
                        $"Cannot extract {typeof(T).Name} argument: JSON String '{s}' is not a numeric literal.");
                default:
                    throw new InvalidOperationException(
                        $"Cannot extract {typeof(T).Name} argument: unexpected JsonValueKind {je.ValueKind}.");
            }
        }

        throw new InvalidOperationException(
            $"Cannot extract {typeof(T).Name} argument from {raw?.GetType().FullName ?? "null"}.");
    }
}
