using System.Text.Json;

namespace NexusLabs.Needlr.AgentFramework.Tests;

public class AgentFrameworkArgumentExtractorTests
{
    private static JsonElement ParseElement(string json) =>
        JsonDocument.Parse(json).RootElement.Clone();

    [Fact]
    public void GetStringArgument_FromStringElement_ReturnsString()
    {
        var je = ParseElement("\"hello\"");

        Assert.Equal("hello", AgentFrameworkArgumentExtractor.GetStringArgument(je));
    }

    [Fact]
    public void GetStringArgument_FromArrayElement_ReturnsCanonicalJson()
    {
        var je = ParseElement("[1,2,3]");

        Assert.Equal("[1,2,3]", AgentFrameworkArgumentExtractor.GetStringArgument(je));
    }

    [Fact]
    public void GetStringArgument_FromObjectElement_ReturnsCanonicalJson()
    {
        var je = ParseElement("{\"a\":1}");

        Assert.Equal("{\"a\":1}", AgentFrameworkArgumentExtractor.GetStringArgument(je));
    }

    [Fact]
    public void GetStringArgument_FromNumberElement_ReturnsCanonicalJson()
    {
        var je = ParseElement("42");

        Assert.Equal("42", AgentFrameworkArgumentExtractor.GetStringArgument(je));
    }

    [Fact]
    public void GetStringArgument_FromBooleanElement_ReturnsCanonicalJson()
    {
        var je = ParseElement("true");

        Assert.Equal("true", AgentFrameworkArgumentExtractor.GetStringArgument(je));
    }

    [Fact]
    public void GetStringArgument_FromNullElement_Throws()
    {
        var je = ParseElement("null");

        var ex = Assert.Throws<InvalidOperationException>(
            () => AgentFrameworkArgumentExtractor.GetStringArgument(je));
        Assert.Contains("string argument", ex.Message);
    }

    [Fact]
    public void GetStringArgument_FromNull_Throws()
    {
        var ex = Assert.Throws<InvalidOperationException>(
            () => AgentFrameworkArgumentExtractor.GetStringArgument(null));
        Assert.Contains("string argument", ex.Message);
    }

    [Fact]
    public void GetStringArgument_FromUndefinedElement_Throws()
    {
        var je = default(JsonElement);

        var ex = Assert.Throws<InvalidOperationException>(
            () => AgentFrameworkArgumentExtractor.GetStringArgument(je));
        Assert.Contains("string argument", ex.Message);
    }

    [Fact]
    public void GetStringArgument_FromTypedString_ReturnsAsIs()
    {
        Assert.Equal("hello", AgentFrameworkArgumentExtractor.GetStringArgument("hello"));
    }

    [Fact]
    public void GetStringArgument_FromOtherObject_UsesToString()
    {
        Assert.Equal("42", AgentFrameworkArgumentExtractor.GetStringArgument(42));
    }

    [Fact]
    public void GetBooleanArgument_FromTrueElement_ReturnsTrue()
    {
        Assert.True(AgentFrameworkArgumentExtractor.GetBooleanArgument(ParseElement("true")));
    }

    [Fact]
    public void GetBooleanArgument_FromFalseElement_ReturnsFalse()
    {
        Assert.False(AgentFrameworkArgumentExtractor.GetBooleanArgument(ParseElement("false")));
    }

    [Fact]
    public void GetBooleanArgument_FromStringTrueLiteral_ReturnsTrue()
    {
        Assert.True(AgentFrameworkArgumentExtractor.GetBooleanArgument(ParseElement("\"true\"")));
    }

    [Fact]
    public void GetBooleanArgument_FromStringFalseLiteral_ReturnsFalse()
    {
        Assert.False(AgentFrameworkArgumentExtractor.GetBooleanArgument(ParseElement("\"false\"")));
    }

    [Fact]
    public void GetBooleanArgument_FromStringMixedCase_ReturnsParsed()
    {
        Assert.True(AgentFrameworkArgumentExtractor.GetBooleanArgument(ParseElement("\"True\"")));
    }

    [Fact]
    public void GetBooleanArgument_FromNumber_Throws()
    {
        var je = ParseElement("1");

        Assert.Throws<InvalidOperationException>(() =>
            AgentFrameworkArgumentExtractor.GetBooleanArgument(je));
    }

    [Fact]
    public void GetBooleanArgument_FromStringOne_Throws()
    {
        var je = ParseElement("\"1\"");

        Assert.Throws<InvalidOperationException>(() =>
            AgentFrameworkArgumentExtractor.GetBooleanArgument(je));
    }

    [Fact]
    public void GetBooleanArgument_FromStringYes_Throws()
    {
        var je = ParseElement("\"yes\"");

        Assert.Throws<InvalidOperationException>(() =>
            AgentFrameworkArgumentExtractor.GetBooleanArgument(je));
    }

    [Fact]
    public void GetBooleanArgument_FromArray_Throws()
    {
        var je = ParseElement("[]");

        Assert.Throws<InvalidOperationException>(() =>
            AgentFrameworkArgumentExtractor.GetBooleanArgument(je));
    }

    [Fact]
    public void GetBooleanArgument_FromNullElement_Throws()
    {
        var je = ParseElement("null");

        Assert.Throws<InvalidOperationException>(() =>
            AgentFrameworkArgumentExtractor.GetBooleanArgument(je));
    }

    [Fact]
    public void GetBooleanArgument_FromTypedBool_ReturnsAsIs()
    {
        Assert.True(AgentFrameworkArgumentExtractor.GetBooleanArgument(true));
        Assert.False(AgentFrameworkArgumentExtractor.GetBooleanArgument(false));
    }

    [Fact]
    public void GetBooleanArgument_FromStringObject_Throws()
    {
        Assert.Throws<InvalidOperationException>(() =>
            AgentFrameworkArgumentExtractor.GetBooleanArgument("true"));
    }

    [Fact]
    public void GetInt32Argument_FromNumberElement_ReturnsValue()
    {
        Assert.Equal(42, AgentFrameworkArgumentExtractor.GetInt32Argument(ParseElement("42")));
    }

    [Fact]
    public void GetInt32Argument_FromStringElement_ParsesInvariant()
    {
        Assert.Equal(42, AgentFrameworkArgumentExtractor.GetInt32Argument(ParseElement("\"42\"")));
    }

    [Fact]
    public void GetInt32Argument_FromFractionalNumber_Throws()
    {
        var je = ParseElement("3.14");

        Assert.Throws<InvalidOperationException>(() =>
            AgentFrameworkArgumentExtractor.GetInt32Argument(je));
    }

    [Fact]
    public void GetInt32Argument_FromOutOfRangeNumber_Throws()
    {
        var je = ParseElement("999999999999999");

        Assert.Throws<InvalidOperationException>(() =>
            AgentFrameworkArgumentExtractor.GetInt32Argument(je));
    }

    [Fact]
    public void GetInt32Argument_FromUnparseableString_Throws()
    {
        var je = ParseElement("\"abc\"");

        Assert.Throws<InvalidOperationException>(() =>
            AgentFrameworkArgumentExtractor.GetInt32Argument(je));
    }

    [Fact]
    public void GetInt32Argument_FromBoolean_Throws()
    {
        var je = ParseElement("true");

        Assert.Throws<InvalidOperationException>(() =>
            AgentFrameworkArgumentExtractor.GetInt32Argument(je));
    }

    [Fact]
    public void GetInt32Argument_FromTypedInt_ReturnsAsIs()
    {
        Assert.Equal(42, AgentFrameworkArgumentExtractor.GetInt32Argument(42));
    }

    [Fact]
    public void GetInt32Argument_FromTypedLong_DoesNotSilentlyNarrow()
    {
        long bigger = 42L;

        // Strict typing: long is not int. Helper does not silently narrow.
        Assert.Throws<InvalidOperationException>(() =>
            AgentFrameworkArgumentExtractor.GetInt32Argument(bigger));
    }

    [Fact]
    public void GetInt64Argument_FromLargeNumber_Returns()
    {
        Assert.Equal(999999999999L,
            AgentFrameworkArgumentExtractor.GetInt64Argument(ParseElement("999999999999")));
    }

    [Fact]
    public void GetInt16Argument_FromValidNumber_Returns()
    {
        Assert.Equal((short)42, AgentFrameworkArgumentExtractor.GetInt16Argument(ParseElement("42")));
    }

    [Fact]
    public void GetByteArgument_FromValidNumber_Returns()
    {
        Assert.Equal((byte)42, AgentFrameworkArgumentExtractor.GetByteArgument(ParseElement("42")));
    }

    [Fact]
    public void GetUInt32Argument_FromValidNumber_Returns()
    {
        Assert.Equal(42u, AgentFrameworkArgumentExtractor.GetUInt32Argument(ParseElement("42")));
    }

    [Fact]
    public void GetSingleArgument_FromNumber_Returns()
    {
        Assert.Equal(3.14f, AgentFrameworkArgumentExtractor.GetSingleArgument(ParseElement("3.14")));
    }

    [Fact]
    public void GetSingleArgument_FromString_ParsesInvariant()
    {
        Assert.Equal(3.14f, AgentFrameworkArgumentExtractor.GetSingleArgument(ParseElement("\"3.14\"")));
    }

    [Fact]
    public void GetSingleArgument_FromTypedNaN_Throws()
    {
        Assert.Throws<InvalidOperationException>(() =>
            AgentFrameworkArgumentExtractor.GetSingleArgument(float.NaN));
    }

    [Fact]
    public void GetSingleArgument_FromTypedInfinity_Throws()
    {
        Assert.Throws<InvalidOperationException>(() =>
            AgentFrameworkArgumentExtractor.GetSingleArgument(float.PositiveInfinity));
    }

    [Fact]
    public void GetDoubleArgument_FromNumber_PrefersTryGetDouble()
    {
        Assert.Equal(2.718281828, AgentFrameworkArgumentExtractor.GetDoubleArgument(ParseElement("2.718281828")));
    }

    [Fact]
    public void GetDoubleArgument_FromTypedNaN_Throws()
    {
        Assert.Throws<InvalidOperationException>(() =>
            AgentFrameworkArgumentExtractor.GetDoubleArgument(double.NaN));
    }

    [Fact]
    public void GetDecimalArgument_FromNumber_PreservesPrecision()
    {
        // A value that loses precision through double but not through decimal
        var je = ParseElement("0.1");

        var result = AgentFrameworkArgumentExtractor.GetDecimalArgument(je);

        Assert.Equal(0.1m, result);
    }

    [Fact]
    public void GetDecimalArgument_FromString_ParsesInvariant()
    {
        Assert.Equal(123.456m,
            AgentFrameworkArgumentExtractor.GetDecimalArgument(ParseElement("\"123.456\"")));
    }

    [Fact]
    public void GetDecimalArgument_FromUnparseableString_Throws()
    {
        Assert.Throws<InvalidOperationException>(() =>
            AgentFrameworkArgumentExtractor.GetDecimalArgument(ParseElement("\"abc\"")));
    }

    [Fact]
    public void GetDecimalArgument_FromTypedDecimal_ReturnsAsIs()
    {
        Assert.Equal(42.5m, AgentFrameworkArgumentExtractor.GetDecimalArgument(42.5m));
    }

    [Fact]
    public void GetGuidArgument_FromStringElement_ReturnsParsedGuid()
    {
        var je = ParseElement("\"d2719b96-4d6e-4f1c-9bff-3a8d6f1c2e7a\"");

        var result = AgentFrameworkArgumentExtractor.GetGuidArgument(je);

        Assert.Equal(Guid.Parse("d2719b96-4d6e-4f1c-9bff-3a8d6f1c2e7a"), result);
    }

    [Fact]
    public void GetGuidArgument_FromInvalidString_Throws()
    {
        var je = ParseElement("\"not-a-guid\"");

        Assert.Throws<InvalidOperationException>(() =>
            AgentFrameworkArgumentExtractor.GetGuidArgument(je));
    }

    [Fact]
    public void GetGuidArgument_FromTypedGuid_ReturnsAsIs()
    {
        var g = Guid.NewGuid();

        Assert.Equal(g, AgentFrameworkArgumentExtractor.GetGuidArgument(g));
    }

    [Fact]
    public void GetGuidArgument_FromNumberKind_Throws()
    {
        var je = ParseElement("42");

        Assert.Throws<InvalidOperationException>(() =>
            AgentFrameworkArgumentExtractor.GetGuidArgument(je));
    }

    [Fact]
    public void GetGuidArgument_FromNullElement_Throws()
    {
        var je = ParseElement("null");

        Assert.Throws<InvalidOperationException>(() =>
            AgentFrameworkArgumentExtractor.GetGuidArgument(je));
    }

    [Fact]
    public void GetGuidArgument_FromNull_Throws()
    {
        Assert.Throws<InvalidOperationException>(() =>
            AgentFrameworkArgumentExtractor.GetGuidArgument(null));
    }

    [Fact]
    public void GetDateTimeArgument_FromIso8601String_ReturnsParsedDateTime()
    {
        var je = ParseElement("\"2026-05-05T18:30:00Z\"");

        var result = AgentFrameworkArgumentExtractor.GetDateTimeArgument(je);

        Assert.Equal(2026, result.Year);
        Assert.Equal(5, result.Month);
        Assert.Equal(5, result.Day);
    }

    [Fact]
    public void GetDateTimeArgument_FromInvalidString_Throws()
    {
        var je = ParseElement("\"not-a-date\"");

        Assert.Throws<InvalidOperationException>(() =>
            AgentFrameworkArgumentExtractor.GetDateTimeArgument(je));
    }

    [Fact]
    public void GetDateTimeArgument_FromTypedDateTime_ReturnsAsIs()
    {
        var dt = new DateTime(2026, 5, 5, 18, 30, 0, DateTimeKind.Utc);

        Assert.Equal(dt, AgentFrameworkArgumentExtractor.GetDateTimeArgument(dt));
    }

    [Fact]
    public void GetDateTimeArgument_FromNumberKind_Throws()
    {
        Assert.Throws<InvalidOperationException>(() =>
            AgentFrameworkArgumentExtractor.GetDateTimeArgument(ParseElement("1735689600")));
    }

    [Fact]
    public void GetDateTimeOffsetArgument_FromIso8601StringWithOffset_ReturnsParsedDateTimeOffset()
    {
        var je = ParseElement("\"2026-05-05T18:30:00+05:00\"");

        var result = AgentFrameworkArgumentExtractor.GetDateTimeOffsetArgument(je);

        Assert.Equal(TimeSpan.FromHours(5), result.Offset);
        Assert.Equal(2026, result.Year);
    }

    [Fact]
    public void GetDateTimeOffsetArgument_FromInvalidString_Throws()
    {
        Assert.Throws<InvalidOperationException>(() =>
            AgentFrameworkArgumentExtractor.GetDateTimeOffsetArgument(ParseElement("\"nonsense\"")));
    }

    [Fact]
    public void GetDateTimeOffsetArgument_FromTypedDateTimeOffset_ReturnsAsIs()
    {
        var dto = new DateTimeOffset(2026, 5, 5, 18, 30, 0, TimeSpan.FromHours(-7));

        Assert.Equal(dto, AgentFrameworkArgumentExtractor.GetDateTimeOffsetArgument(dto));
    }

    [Fact]
    public void GetTimeSpanArgument_FromDotNetRoundTripFormat_ReturnsParsed()
    {
        var je = ParseElement("\"01:30:00\"");

        var result = AgentFrameworkArgumentExtractor.GetTimeSpanArgument(je);

        Assert.Equal(TimeSpan.FromMinutes(90), result);
    }

    [Fact]
    public void GetTimeSpanArgument_FromIso8601DurationFormat_ReturnsParsed()
    {
        var je = ParseElement("\"PT1H30M\"");

        var result = AgentFrameworkArgumentExtractor.GetTimeSpanArgument(je);

        Assert.Equal(TimeSpan.FromMinutes(90), result);
    }

    [Fact]
    public void GetTimeSpanArgument_FromIso8601LongerDuration_ReturnsParsed()
    {
        var je = ParseElement("\"P1DT2H3M4S\"");

        var result = AgentFrameworkArgumentExtractor.GetTimeSpanArgument(je);

        Assert.Equal(new TimeSpan(1, 2, 3, 4), result);
    }

    [Fact]
    public void GetTimeSpanArgument_FromInvalidString_Throws()
    {
        Assert.Throws<InvalidOperationException>(() =>
            AgentFrameworkArgumentExtractor.GetTimeSpanArgument(ParseElement("\"abc\"")));
    }

    [Fact]
    public void GetTimeSpanArgument_FromTypedTimeSpan_ReturnsAsIs()
    {
        var ts = TimeSpan.FromMinutes(90);

        Assert.Equal(ts, AgentFrameworkArgumentExtractor.GetTimeSpanArgument(ts));
    }

    [Fact]
    public void GetTimeSpanArgument_FromNumberKind_Throws()
    {
        Assert.Throws<InvalidOperationException>(() =>
            AgentFrameworkArgumentExtractor.GetTimeSpanArgument(ParseElement("3600")));
    }

    [Fact]
    public void GetTimeSpanArgument_FromNullElement_Throws()
    {
        Assert.Throws<InvalidOperationException>(() =>
            AgentFrameworkArgumentExtractor.GetTimeSpanArgument(ParseElement("null")));
    }

    [Fact]
    public void IsArgumentSupplied_FromNull_ReturnsFalse()
    {
        Assert.False(AgentFrameworkArgumentExtractor.IsArgumentSupplied(null));
    }

    [Fact]
    public void IsArgumentSupplied_FromJsonNullElement_ReturnsFalse()
    {
        Assert.False(AgentFrameworkArgumentExtractor.IsArgumentSupplied(ParseElement("null")));
    }

    [Fact]
    public void IsArgumentSupplied_FromUndefinedJsonElement_ReturnsFalse()
    {
        Assert.False(AgentFrameworkArgumentExtractor.IsArgumentSupplied(default(JsonElement)));
    }

    [Fact]
    public void IsArgumentSupplied_FromTypedString_ReturnsTrue()
    {
        Assert.True(AgentFrameworkArgumentExtractor.IsArgumentSupplied("hello"));
    }

    [Fact]
    public void IsArgumentSupplied_FromTypedBool_ReturnsTrue()
    {
        Assert.True(AgentFrameworkArgumentExtractor.IsArgumentSupplied(true));
    }

    [Fact]
    public void IsArgumentSupplied_FromJsonStringElement_ReturnsTrue()
    {
        Assert.True(AgentFrameworkArgumentExtractor.IsArgumentSupplied(ParseElement("\"hi\"")));
    }

    [Fact]
    public void IsArgumentSupplied_FromJsonNumberElement_ReturnsTrue()
    {
        Assert.True(AgentFrameworkArgumentExtractor.IsArgumentSupplied(ParseElement("42")));
    }

    [Fact]
    public void IsArgumentSupplied_FromJsonBooleanElement_ReturnsTrue()
    {
        Assert.True(AgentFrameworkArgumentExtractor.IsArgumentSupplied(ParseElement("true")));
    }

    [Fact]
    public void IsArgumentSupplied_FromJsonObjectElement_ReturnsTrue()
    {
        Assert.True(AgentFrameworkArgumentExtractor.IsArgumentSupplied(ParseElement("{\"k\":1}")));
    }

    [Fact]
    public void IsArgumentSupplied_FromJsonArrayElement_ReturnsTrue()
    {
        Assert.True(AgentFrameworkArgumentExtractor.IsArgumentSupplied(ParseElement("[1,2,3]")));
    }
}
