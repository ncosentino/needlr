namespace NexusLabs.Needlr.AgentFramework.Langfuse.Tests;

public sealed class LangfuseTelemetryDisabledTests
{
    [Fact]
    public void Start_WithoutCredentials_ReturnsDisabledNoOpSession()
    {
        using var session = LangfuseTelemetry.Start(new LangfuseOptions());

        Assert.False(session.IsEnabled);
        Assert.True(session.Flush());

        var first = session.Shutdown(TimeSpan.Zero);
        var second = session.Shutdown(TimeSpan.Zero);

        Assert.True(first.IsFinal, "Expected disabled shutdown to complete synchronously.");
        Assert.Equal(LangfuseProviderShutdownStatus.NotConfigured, first.Traces);
        Assert.Equal(LangfuseProviderShutdownStatus.NotConfigured, first.Metrics);
        Assert.Same(first, second);
    }

    [Fact]
    public async Task DisabledScenario_RecordsScoresWithoutThrowingOrCallingNetwork()
    {
        using var session = LangfuseTelemetry.Start(new LangfuseOptions());
        using var scenario = session.BeginScenario("scenario", sessionId: "run-1", tags: ["smoke"]);

        Assert.Null(scenario.TraceId);
        Assert.Null(scenario.Activity);

        await scenario.RecordScoreAsync("numeric", 0.5, cancellationToken: TestContext.Current.CancellationToken);
        await scenario.RecordScoreAsync("boolean", true, cancellationToken: TestContext.Current.CancellationToken);
        await scenario.RecordScoreAsync("categorical", "good", cancellationToken: TestContext.Current.CancellationToken);
    }

    [Fact]
    public void Start_WithEnabledFalse_IsDisabledEvenWithKeys()
    {
        var options = new LangfuseOptions { PublicKey = "pk", SecretKey = "sk", Region = LangfuseRegion.Eu, Enabled = false };

        using var session = LangfuseTelemetry.Start(options);

        Assert.False(session.IsEnabled);
    }

    [Fact]
    public void Start_WithKeysButNoTarget_IsDisabledAndWarnsAboutCloudEgress()
    {
        string? warning = null;
        var options = new LangfuseOptions
        {
            PublicKey = "pk",
            SecretKey = "sk",
            DiagnosticsCallback = msg => warning = msg,
        };

        using var session = LangfuseTelemetry.Start(options);

        Assert.False(session.IsEnabled);
        Assert.NotNull(warning);
        Assert.Contains("no export target", warning!, StringComparison.OrdinalIgnoreCase);
    }
}
