using Microsoft.Extensions.DependencyInjection;

using OpenTelemetry.Trace;

namespace NexusLabs.Needlr.AgentFramework.Langfuse.Tests;

public sealed class LangfuseServiceCollectionExtensionsTests
{
    [Fact]
    public void AddNeedlrLangfuse_Configured_RegistersEnabledScoreClientAndTracerProvider()
    {
        var services = new ServiceCollection();

        services.AddNeedlrLangfuse(o =>
        {
            o.PublicKey = "pk-lf-1";
            o.SecretKey = "sk-lf-2";
            o.Host = "http://localhost:3000";
        });

        using var provider = services.BuildServiceProvider();

        var scoreClient = provider.GetService<ILangfuseScoreClient>();
        Assert.NotNull(scoreClient);
        Assert.True(scoreClient!.IsEnabled);

        // AddOpenTelemetry registers a TracerProvider when tracing is configured.
        Assert.NotNull(provider.GetService<TracerProvider>());
    }

    [Fact]
    public void AddNeedlrLangfuse_NotConfigured_RegistersDisabledNoOpScoreClient()
    {
        var services = new ServiceCollection();

        // Keys present but no Host/Region → not configured → disabled (no cloud egress).
        services.AddNeedlrLangfuse(o =>
        {
            o.PublicKey = "pk-lf-1";
            o.SecretKey = "sk-lf-2";
            o.Host = null;
            o.Region = null;
        });

        using var provider = services.BuildServiceProvider();

        var scoreClient = provider.GetService<ILangfuseScoreClient>();
        Assert.NotNull(scoreClient);
        Assert.False(scoreClient!.IsEnabled);
    }

    [Fact]
    public async Task AddNeedlrLangfuse_DisabledScoreClient_RecordIsNoOp()
    {
        var services = new ServiceCollection();
        services.AddNeedlrLangfuse(o => { o.Enabled = false; });

        using var provider = services.BuildServiceProvider();
        var scoreClient = provider.GetRequiredService<ILangfuseScoreClient>();

        await scoreClient.RecordScoreAsync("trace", "name", 1.0, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(0, scoreClient.ScoresFailed);
    }
}
