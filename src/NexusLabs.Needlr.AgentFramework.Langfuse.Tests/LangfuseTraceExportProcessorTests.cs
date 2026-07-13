using System.Diagnostics;

using OpenTelemetry;

namespace NexusLabs.Needlr.AgentFramework.Langfuse.Tests;

public sealed class LangfuseTraceExportProcessorTests
{
    private readonly CancellationToken _cancellationToken = TestContext.Current.CancellationToken;

    [Fact]
    public void QueueSaturation_ReportsObservedEnqueuedDroppedAndAcknowledgedSeparately()
    {
        var exporter = new ControlledExporter<Activity>();
        exporter.BlockExport();
        var health = new LangfusePublicationHealth(isEnabled: true);
        using var processor = new LangfuseBatchActivityExportProcessor(
            exporter,
            health,
            maxQueueSize: 2,
            scheduledDelayMilliseconds: 60_000,
            maxExportBatchSize: 1);

        processor.OnEnd(CreateRecordedActivity("first"));
        exporter.WaitForExport(_cancellationToken);
        processor.OnEnd(CreateRecordedActivity("second"));
        processor.OnEnd(CreateRecordedActivity("third"));
        processor.OnEnd(CreateRecordedActivity("dropped"));

        var saturated = health.GetSnapshot().TraceExport;
        Assert.Equal(4, saturated.LocallyObserved);
        Assert.Equal(3, saturated.LocallyEnqueued);
        Assert.Equal(1, saturated.Dropped);
        Assert.Equal(0, saturated.Acknowledged);

        exporter.ReleaseExport();
        Assert.True(
            processor.ForceFlush(1_000),
            "Expected the processor to drain every item accepted before force flush.");

        var drained = health.GetSnapshot().TraceExport;
        Assert.Equal(3, drained.Acknowledged);
        Assert.Equal(0, drained.Failed);
        Assert.Equal(3, drained.SuccessfulBatches);
    }

    [Fact]
    public void ExportFailure_ReportsFailedBatchWithoutClaimingAcknowledgement()
    {
        var exporter = new ControlledExporter<Activity>
        {
            ExportResult = ExportResult.Failure,
        };
        var health = new LangfusePublicationHealth(isEnabled: true);
        using var processor = new LangfuseBatchActivityExportProcessor(
            exporter,
            health,
            maxQueueSize: 4,
            scheduledDelayMilliseconds: 1,
            maxExportBatchSize: 4);

        processor.OnEnd(CreateRecordedActivity("failed"));

        Assert.True(
            processor.ForceFlush(1_000),
            "Expected force flush to remove the failed batch from the local queue.");
        var trace = health.GetSnapshot().TraceExport;
        Assert.Equal(0, trace.Acknowledged);
        Assert.Equal(1, trace.Failed);
        Assert.Equal(0, trace.SuccessfulBatches);
        Assert.Equal(1, trace.FailedBatches);
    }

    private static Activity CreateRecordedActivity(string name)
    {
        var activity = new Activity(name);
        activity.SetIdFormat(ActivityIdFormat.W3C);
        activity.ActivityTraceFlags = ActivityTraceFlags.Recorded;
        activity.Start();
        activity.Stop();
        return activity;
    }
}
