using System.Reflection;

using NexusLabs.Needlr.AgentFramework.Evaluation.Experiments;

namespace NexusLabs.Needlr.AgentFramework.Evaluation.Tests.Experiments;

public sealed class ExperimentFailureTests
{
    [Fact]
    public void Constructor_ValidArguments_PopulatesEveryProperty()
    {
        var failure = new ExperimentFailure(
            ExperimentFailureCode.ExecutionFailed,
            ExperimentFailureStage.Execution,
            "System.InvalidOperationException",
            "boom",
            isRetryable: true);

        Assert.Equal(ExperimentFailureCode.ExecutionFailed, failure.Code);
        Assert.Equal(ExperimentFailureStage.Execution, failure.Stage);
        Assert.Equal("System.InvalidOperationException", failure.ExceptionType);
        Assert.Equal("boom", failure.Message);
        Assert.True(failure.IsRetryable, "Expected the retryable flag to be preserved.");
    }

    [Fact]
    public void Constructor_EmptyMessage_IsPreserved()
    {
        var failure = new ExperimentFailure(
            ExperimentFailureCode.PolicyFailed,
            ExperimentFailureStage.Policy,
            "System.Exception",
            string.Empty,
            isRetryable: false);

        Assert.Equal(string.Empty, failure.Message);
        Assert.False(failure.IsRetryable, "Expected the non-retryable flag to be preserved.");
    }

    [Fact]
    public void Constructor_UndefinedCode_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ExperimentFailure(
            (ExperimentFailureCode)999,
            ExperimentFailureStage.Execution,
            "System.Exception",
            "message",
            isRetryable: false));
    }

    [Fact]
    public void Constructor_UndefinedStage_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ExperimentFailure(
            ExperimentFailureCode.ExecutionFailed,
            (ExperimentFailureStage)999,
            "System.Exception",
            "message",
            isRetryable: false));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_BlankExceptionType_Throws(string? exceptionType)
    {
        Assert.ThrowsAny<ArgumentException>(() => new ExperimentFailure(
            ExperimentFailureCode.ExecutionFailed,
            ExperimentFailureStage.Execution,
            exceptionType!,
            "message",
            isRetryable: false));
    }

    [Fact]
    public void Constructor_NullMessage_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new ExperimentFailure(
            ExperimentFailureCode.ExecutionFailed,
            ExperimentFailureStage.Execution,
            "System.Exception",
            null!,
            isRetryable: false));
    }

    [Fact]
    public void Contract_IsSealedRecordWithReadOnlyProperties()
    {
        var type = typeof(ExperimentFailure);
        Assert.True(type.IsSealed, "Expected structured failures to remain sealed.");
        Assert.DoesNotContain(
            type.GetConstructors(),
            constructor => constructor.GetParameters().Length == 0);
        var properties = type.GetProperties(
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly);
        Assert.All(
            properties,
            property => Assert.False(
                property.SetMethod?.IsPublic ?? false,
                $"Expected property '{property.Name}' to remain read-only."));
    }
}
