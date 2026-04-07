using NexusLabs.Needlr.AgentFramework.Collectors;

namespace NexusLabs.Needlr.AgentFramework.Tests;

public class AgentOutputCollectorTests
{
    // -------------------------------------------------------------------------
    // AgentOutputCollector<T>
    // -------------------------------------------------------------------------

    [Fact]
    public void Add_AccumulatesItems()
    {
        var collector = new AgentOutputCollector<string>();

        collector.Add("item-1");
        collector.Add("item-2");

        Assert.Equal(2, collector.Count);
        Assert.Equal(["item-1", "item-2"], collector.Items);
    }

    [Fact]
    public void Items_ReturnsSnapshot()
    {
        var collector = new AgentOutputCollector<int>();
        collector.Add(1);

        var snapshot = collector.Items;
        collector.Add(2);

        // Snapshot should not include items added after it was taken
        Assert.Single(snapshot);
    }

    [Fact]
    public void Empty_Collector_HasZeroCount()
    {
        var collector = new AgentOutputCollector<string>();

        Assert.Equal(0, collector.Count);
        Assert.Empty(collector.Items);
    }

    [Fact]
    public async Task ConcurrentAdds_AllItemsCollected()
    {
        var collector = new AgentOutputCollector<int>();
        var ct = TestContext.Current.CancellationToken;

        var tasks = Enumerable.Range(0, 100).Select(i =>
            Task.Run(() => collector.Add(i), ct));

        await Task.WhenAll(tasks);

        Assert.Equal(100, collector.Count);
    }

    // -------------------------------------------------------------------------
    // AgentOutputCollectorAccessor<T>
    // -------------------------------------------------------------------------

    [Fact]
    public void Current_WithoutScope_ReturnsNull()
    {
        var accessor = new AgentOutputCollectorAccessor<string>();

        Assert.Null(accessor.Current);
    }

    [Fact]
    public void BeginScope_SetsCurrent()
    {
        var accessor = new AgentOutputCollectorAccessor<string>();

        using var scope = accessor.BeginScope();

        Assert.NotNull(accessor.Current);
    }

    [Fact]
    public void BeginScope_WithCollector_UsesThatCollector()
    {
        var accessor = new AgentOutputCollectorAccessor<string>();
        var collector = new AgentOutputCollector<string>();

        using var scope = accessor.BeginScope(collector);

        Assert.Same(collector, accessor.Current);
    }

    [Fact]
    public void Dispose_RestoresPreviousScope()
    {
        var accessor = new AgentOutputCollectorAccessor<string>();

        using (accessor.BeginScope())
        {
            var outer = accessor.Current;

            using (accessor.BeginScope())
            {
                Assert.NotSame(outer, accessor.Current);
            }

            Assert.Same(outer, accessor.Current);
        }

        Assert.Null(accessor.Current);
    }

    [Fact]
    public void Add_InsideScope_IsVisible()
    {
        var accessor = new AgentOutputCollectorAccessor<string>();

        using (accessor.BeginScope())
        {
            accessor.Current!.Add("hello");

            Assert.Single(accessor.Current.Items);
            Assert.Equal("hello", accessor.Current.Items[0]);
        }
    }

    [Fact]
    public async Task ConcurrentScopes_AreIsolated()
    {
        var accessor = new AgentOutputCollectorAccessor<string>();
        var ct = TestContext.Current.CancellationToken;

        int countInTask1 = 0;
        int countInTask2 = 0;

        var task1 = Task.Run(() =>
        {
            using (accessor.BeginScope())
            {
                accessor.Current!.Add("a");
                accessor.Current!.Add("b");
                countInTask1 = accessor.Current.Count;
            }
        }, ct);

        var task2 = Task.Run(() =>
        {
            using (accessor.BeginScope())
            {
                accessor.Current!.Add("x");
                countInTask2 = accessor.Current.Count;
            }
        }, ct);

        await Task.WhenAll(task1, task2);

        Assert.Equal(2, countInTask1);
        Assert.Equal(1, countInTask2);
    }

    [Fact]
    public void BeginScope_NullCollector_ThrowsArgumentNull()
    {
        var accessor = new AgentOutputCollectorAccessor<string>();

        Assert.Throws<ArgumentNullException>(() =>
            accessor.BeginScope(null!));
    }
}
