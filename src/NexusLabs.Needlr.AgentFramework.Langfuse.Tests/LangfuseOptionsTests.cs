namespace NexusLabs.Needlr.AgentFramework.Langfuse.Tests;

public sealed class LangfuseOptionsTests
{
    [Fact]
    public void IsConfigured_RequiresKeysEnabledAndExplicitTarget()
    {
        Assert.False(new LangfuseOptions().IsConfigured);
        Assert.False(new LangfuseOptions { PublicKey = "pk" }.IsConfigured);
        Assert.False(new LangfuseOptions { SecretKey = "sk" }.IsConfigured);

        // Keys present but no Host/Region → not configured (no accidental cloud egress).
        Assert.False(new LangfuseOptions { PublicKey = "pk", SecretKey = "sk" }.IsConfigured);

        // Explicit target enables it.
        Assert.True(new LangfuseOptions { PublicKey = "pk", SecretKey = "sk", Host = "http://localhost:3000" }.IsConfigured);
        Assert.True(new LangfuseOptions { PublicKey = "pk", SecretKey = "sk", Region = LangfuseRegion.Eu }.IsConfigured);

        Assert.False(new LangfuseOptions { PublicKey = "pk", SecretKey = "sk", Region = LangfuseRegion.Eu, Enabled = false }.IsConfigured);
    }

    [Fact]
    public void HasCredentials_IsIndependentOfTarget()
    {
        Assert.True(new LangfuseOptions { PublicKey = "pk", SecretKey = "sk" }.HasCredentials);
        Assert.False(new LangfuseOptions { PublicKey = "pk", SecretKey = "sk" }.HasExplicitTarget);
    }

    [Fact]
    public void Defaults_MatchNeedlrTelemetrySourceNames()
    {
        var options = new LangfuseOptions();

        Assert.Equal("NexusLabs.Needlr.AgentFramework", options.AgentActivitySourceName);
        Assert.Equal("NexusLabs.Needlr.AgentFramework", options.AgentMeterName);
        Assert.Equal("Experimental.Microsoft.Extensions.AI", options.GenAiMeterName);
        Assert.Null(options.Region);
        Assert.False(options.IncludeMetrics);
        Assert.False(options.NormalizeScoreNames);
        Assert.Equal(LangfuseScoreFailureMode.NonFatal, options.ScoreFailureMode);
        Assert.Equal(1.0, options.SamplingRatio);
        Assert.Equal(TimeSpan.FromSeconds(5), options.ShutdownTimeout);
        Assert.Equal(TimeSpan.FromSeconds(30), options.Http.RequestTimeout);
        Assert.Equal(3, options.Http.MaxAttempts);
        Assert.Equal(TimeSpan.FromMilliseconds(200), options.Http.InitialRetryDelay);
        Assert.Equal(TimeSpan.FromSeconds(5), options.Http.MaxRetryDelay);
        Assert.Equal(2048, options.TraceExport.MaxQueueSize);
        Assert.Equal(TimeSpan.FromSeconds(5), options.TraceExport.ScheduledDelay);
        Assert.Equal(512, options.TraceExport.MaxBatchSize);
        Assert.Equal(TimeSpan.FromSeconds(30), options.TraceExport.ExporterTimeout);
        Assert.IsType<LangfuseInProcessResourceLockProvider>(options.ResourceLockProvider);
    }

    [Fact]
    public void FromEnvironment_ReadsKeysAndHost()
    {
        using var _ = new EnvironmentVariableScope(new Dictionary<string, string?>
        {
            [LangfuseOptions.PublicKeyEnvironmentVariable] = "pk-env",
            [LangfuseOptions.SecretKeyEnvironmentVariable] = "sk-env",
            [LangfuseOptions.HostEnvironmentVariable] = "http://localhost:3000",
        });

        var options = LangfuseOptions.FromEnvironment();

        Assert.Equal("pk-env", options.PublicKey);
        Assert.Equal("sk-env", options.SecretKey);
        Assert.Equal("http://localhost:3000", options.Host);
        Assert.True(options.IsConfigured);
    }

    [Fact]
    public void FromEnvironment_BlankKeys_AreTreatedAsAbsent()
    {
        using var _ = new EnvironmentVariableScope(new Dictionary<string, string?>
        {
            [LangfuseOptions.PublicKeyEnvironmentVariable] = "   ",
            [LangfuseOptions.SecretKeyEnvironmentVariable] = null,
            [LangfuseOptions.HostEnvironmentVariable] = null,
        });

        var options = LangfuseOptions.FromEnvironment();

        Assert.Null(options.PublicKey);
        Assert.Null(options.SecretKey);
        Assert.False(options.IsConfigured);
    }

    [Fact]
    public void HttpOptions_InvalidBudgetsAreRejected()
    {
        using var httpClient = new HttpClient();
        Assert.Throws<ArgumentOutOfRangeException>(() => new LangfuseApiClient(
            httpClient,
            new Uri("https://lf.example/"),
            "Basic x",
            new LangfuseHttpOptions { RequestTimeout = TimeSpan.Zero }));
        Assert.Throws<ArgumentOutOfRangeException>(() => new LangfuseApiClient(
            httpClient,
            new Uri("https://lf.example/"),
            "Basic x",
            new LangfuseHttpOptions { MaxAttempts = 0 }));
        Assert.Throws<ArgumentOutOfRangeException>(() => new LangfuseApiClient(
            httpClient,
            new Uri("https://lf.example/"),
            "Basic x",
            new LangfuseHttpOptions { InitialRetryDelay = TimeSpan.FromMilliseconds(-1) }));
        Assert.Throws<ArgumentOutOfRangeException>(() => new LangfuseApiClient(
            httpClient,
            new Uri("https://lf.example/"),
            "Basic x",
            new LangfuseHttpOptions
            {
                InitialRetryDelay = TimeSpan.FromSeconds(2),
                MaxRetryDelay = TimeSpan.FromSeconds(1),
            }));
    }

    [Fact]
    public void TraceExportOptions_InvalidQueueAndDurationsAreRejected()
    {
        var options = new LangfuseOptions
        {
            PublicKey = "pk",
            SecretKey = "sk",
            Host = "https://lf.example",
        };
        options.TraceExport.MaxQueueSize = 1;
        options.TraceExport.MaxBatchSize = 2;

        Assert.Throws<ArgumentOutOfRangeException>(() => LangfuseTelemetry.Start(options));

        options.TraceExport.MaxBatchSize = 1;
        options.TraceExport.ScheduledDelay = TimeSpan.Zero;
        Assert.Throws<ArgumentOutOfRangeException>(() => LangfuseTelemetry.Start(options));

        options.TraceExport.ScheduledDelay = TimeSpan.FromSeconds(1);
        options.TraceExport.ExporterTimeout = Timeout.InfiniteTimeSpan;
        Assert.Throws<ArgumentOutOfRangeException>(() => LangfuseTelemetry.Start(options));
    }
}
