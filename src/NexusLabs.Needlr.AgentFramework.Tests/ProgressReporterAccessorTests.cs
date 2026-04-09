using NexusLabs.Needlr.AgentFramework.Progress;

namespace NexusLabs.Needlr.AgentFramework.Tests;

public class ProgressReporterAccessorTests
{
    [Fact]
    public void Current_WithoutScope_ReturnsNullReporter()
    {
        var accessor = new ProgressReporterAccessor();

        Assert.Same(NullProgressReporter.Instance, accessor.Current);
    }

    [Fact]
    public void BeginScope_SetsCurrent()
    {
        var accessor = new ProgressReporterAccessor();
        var reporter = new ProgressReporter("wf-1", [], new ProgressSequenceProvider());

        using var scope = accessor.BeginScope(reporter);

        Assert.Same(reporter, accessor.Current);
    }

    [Fact]
    public void Dispose_RestoresPrevious()
    {
        var accessor = new ProgressReporterAccessor();
        var outer = new ProgressReporter("wf-1", [], new ProgressSequenceProvider());
        var inner = new ProgressReporter("wf-2", [], new ProgressSequenceProvider());

        using (accessor.BeginScope(outer))
        {
            using (accessor.BeginScope(inner))
            {
                Assert.Same(inner, accessor.Current);
            }

            Assert.Same(outer, accessor.Current);
        }

        Assert.Same(NullProgressReporter.Instance, accessor.Current);
    }

    [Fact]
    public async Task ConcurrentScopes_AreIsolated()
    {
        var accessor = new ProgressReporterAccessor();
        var ct = TestContext.Current.CancellationToken;

        string? wfInTask1 = null;
        string? wfInTask2 = null;

        var task1 = Task.Run(() =>
        {
            var r = new ProgressReporter("wf-A", [], new ProgressSequenceProvider());
            using var scope = accessor.BeginScope(r);
            Thread.Sleep(10);
            wfInTask1 = accessor.Current.WorkflowId;
        }, ct);

        var task2 = Task.Run(() =>
        {
            var r = new ProgressReporter("wf-B", [], new ProgressSequenceProvider());
            using var scope = accessor.BeginScope(r);
            Thread.Sleep(10);
            wfInTask2 = accessor.Current.WorkflowId;
        }, ct);

        await Task.WhenAll(task1, task2);

        Assert.Equal("wf-A", wfInTask1);
        Assert.Equal("wf-B", wfInTask2);
        Assert.Same(NullProgressReporter.Instance, accessor.Current);
    }

    [Fact]
    public void BeginScope_NullReporter_Throws()
    {
        var accessor = new ProgressReporterAccessor();

        Assert.Throws<ArgumentNullException>(() => accessor.BeginScope(null!));
    }
}
