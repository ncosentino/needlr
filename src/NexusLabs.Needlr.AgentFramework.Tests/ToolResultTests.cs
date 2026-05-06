using NexusLabs.Needlr.AgentFramework.Tools;

namespace NexusLabs.Needlr.AgentFramework.Tests;

public class ToolResultTests
{
    // -------------------------------------------------------------------------
    // ToolResult<TValue, TError> — Success path
    // -------------------------------------------------------------------------

    [Fact]
    public void Ok_ReturnsSuccessResult()
    {
        var result = ToolResult<string, ToolError>.Ok("hello");

        Assert.True(result.IsSuccess);
        Assert.Equal("hello", result.Value);
        Assert.Null(result.Error);
        Assert.Null(result.Exception);
        Assert.Null(result.IsTransient);
    }

    [Fact]
    public void Ok_BoxedValue_MatchesValue()
    {
        IToolResult result = ToolResult<int, ToolError>.Ok(42);

        Assert.True(result.IsSuccess);
        Assert.Equal(42, result.BoxedValue);
        Assert.Null(result.BoxedError);
    }

    // -------------------------------------------------------------------------
    // ToolResult<TValue, TError> — Failure path
    // -------------------------------------------------------------------------

    [Fact]
    public void Fail_ReturnsFailureResult()
    {
        var error = new ToolError("something broke");
        var result = ToolResult<string, ToolError>.Fail(error);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Value);
        Assert.Same(error, result.Error);
        Assert.Null(result.Exception);
        Assert.Null(result.IsTransient);
    }

    [Fact]
    public void Fail_WithException_PreservesException()
    {
        var ex = new InvalidOperationException("boom");
        var error = new ToolError("something broke");
        var result = ToolResult<string, ToolError>.Fail(error, ex);

        Assert.False(result.IsSuccess);
        Assert.Same(ex, result.Exception);
    }

    [Fact]
    public void Fail_WithTransientFlag_PreservesFlag()
    {
        var result = ToolResult<string, ToolError>.Fail(
            new ToolError("transient issue"), isTransient: true);

        Assert.True(result.IsTransient);
    }

    [Fact]
    public void Fail_BoxedError_MatchesError()
    {
        var error = new ToolError("fail", "try again");
        IToolResult result = ToolResult<string, ToolError>.Fail(error);

        Assert.False(result.IsSuccess);
        Assert.Null(result.BoxedValue);
        Assert.Same(error, result.BoxedError);
    }

    // -------------------------------------------------------------------------
    // ToolResult static factory — shorthand with ToolError
    // -------------------------------------------------------------------------

    [Fact]
    public void ToolResult_Ok_CreatesSuccessWithToolError()
    {
        var result = ToolResult.Ok("data");

        Assert.True(result.IsSuccess);
        Assert.Equal("data", result.Value);
    }

    [Fact]
    public void ToolResult_Fail_FromMessage_CreatesToolError()
    {
        var result = ToolResult.Fail<string>("oops");

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal("oops", result.Error!.Message);
        Assert.Null(result.Error.Suggestion);
    }

    [Fact]
    public void ToolResult_Fail_FromMessage_WithSuggestion_PreservesSuggestion()
    {
        var result = ToolResult.Fail<string>(
            "oops", suggestion: "try again later");

        Assert.Equal("try again later", result.Error!.Suggestion);
    }

    [Fact]
    public void ToolResult_Fail_FromMessage_WithException_PreservesException()
    {
        var ex = new TimeoutException("timed out");
        var result = ToolResult.Fail<string>("timeout", ex: ex, isTransient: true);

        Assert.Same(ex, result.Exception);
        Assert.True(result.IsTransient);
    }

    [Fact]
    public void ToolResult_Fail_CustomError_CreatesFailureWithCustomShape()
    {
        var customError = new CustomError(404, "not found");
        var result = ToolResult.Fail<string, CustomError>(customError);

        Assert.False(result.IsSuccess);
        Assert.Same(customError, result.Error);
    }

    // -------------------------------------------------------------------------
    // ToolResult.UnhandledFailure
    // -------------------------------------------------------------------------

    [Fact]
    public void UnhandledFailure_ReturnsFailure_WithGenericMessage()
    {
        var ex = new NullReferenceException("bad ref");
        var result = ToolResult.UnhandledFailure(ex);

        Assert.False(result.IsSuccess);
        Assert.Null(result.BoxedValue);
        Assert.Same(ex, result.Exception);
        Assert.Null(result.IsTransient);

        // Error should be a ToolError with a safe generic message
        var error = Assert.IsType<ToolError>(result.BoxedError);
        Assert.Contains("unexpected error", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void UnhandledFailure_DefaultFormat_IncludesExceptionTypeName()
    {
        var ex = new ArgumentOutOfRangeException("paramName", "validation detail");
        var result = ToolResult.UnhandledFailure(ex);

        var error = Assert.IsType<ToolError>(result.BoxedError);
        Assert.Contains(nameof(ArgumentOutOfRangeException), error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void UnhandledFailure_DefaultFormat_DoesNotLeakExceptionMessageBody()
    {
        // The exception message contains a sentinel that should never appear in the LLM-facing payload.
        var sentinel = "SENSITIVE_DB_CONNECTION_STRING=Server=secret;Pwd=hunter2";
        var ex = new InvalidOperationException(sentinel);

        var result = ToolResult.UnhandledFailure(ex);

        var error = Assert.IsType<ToolError>(result.BoxedError);
        Assert.DoesNotContain(sentinel, error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("hunter2", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void UnhandledFailure_CustomFormatter_IsHonored()
    {
        var ex = new TimeoutException("upstream slow");
        var result = ToolResult.UnhandledFailure(
            ex,
            formatter: e => new ToolError($"custom: {e.GetType().Name}", Suggestion: "retry"));

        var error = Assert.IsType<ToolError>(result.BoxedError);
        Assert.Equal($"custom: {nameof(TimeoutException)}", error.Message);
        Assert.Equal("retry", error.Suggestion);
    }

    [Fact]
    public void UnhandledFailure_DefaultFormatter_DistinguishesExceptionTypes()
    {
        var arg = ToolResult.UnhandledFailure(new ArgumentException("a"));
        var timeout = ToolResult.UnhandledFailure(new TimeoutException("b"));

        var argMsg = Assert.IsType<ToolError>(arg.BoxedError).Message;
        var timeoutMsg = Assert.IsType<ToolError>(timeout.BoxedError).Message;

        Assert.NotEqual(argMsg, timeoutMsg);
        Assert.Contains(nameof(ArgumentException), argMsg, StringComparison.Ordinal);
        Assert.Contains(nameof(TimeoutException), timeoutMsg, StringComparison.Ordinal);
    }

    // -------------------------------------------------------------------------
    // ToolError record
    // -------------------------------------------------------------------------

    [Fact]
    public void ToolError_MessageOnly_SuggestionIsNull()
    {
        var error = new ToolError("msg");
        Assert.Equal("msg", error.Message);
        Assert.Null(error.Suggestion);
    }

    [Fact]
    public void ToolError_WithSuggestion_BothFieldsSet()
    {
        var error = new ToolError("msg", "hint");
        Assert.Equal("msg", error.Message);
        Assert.Equal("hint", error.Suggestion);
    }
}

// Test helper — custom error shape
public sealed record CustomError(int Code, string Detail);
