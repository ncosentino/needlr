using Microsoft.Extensions.AI;

namespace NexusLabs.Needlr.AgentFramework.Evaluation.Tests;

public sealed class FileEvaluationCaptureStoreTests : IDisposable
{
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;
    private readonly string _directory;

    public FileEvaluationCaptureStoreTests()
    {
        _directory = Path.Combine(Path.GetTempPath(), "needlr-eval-tests-" + Guid.NewGuid().ToString("N"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public void Constructor_NullOrWhitespacePath_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new FileEvaluationCaptureStore(null!));
        Assert.Throws<ArgumentException>(() => new FileEvaluationCaptureStore(""));
        Assert.Throws<ArgumentException>(() => new FileEvaluationCaptureStore("   "));
    }

    [Fact]
    public async Task TryGetAsync_MissingFile_ReturnsNull()
    {
        var store = new FileEvaluationCaptureStore(_directory);

        var result = await store.TryGetAsync("missing-key", _ct);

        Assert.Null(result);
    }

    [Fact]
    public async Task SaveAsync_ThenTryGetAsync_RoundTripsResponse()
    {
        var store = new FileEvaluationCaptureStore(_directory);
        var original = new ChatResponse(new[]
        {
            new ChatMessage(ChatRole.Assistant, "line 1"),
            new ChatMessage(ChatRole.Assistant, "line 2"),
        })
        {
            ResponseId = "r-1",
            ModelId = "m-1",
        };

        await store.SaveAsync("key-a", original, _ct);
        var loaded = await store.TryGetAsync("key-a", _ct);

        Assert.NotNull(loaded);
        Assert.Equal("r-1", loaded!.ResponseId);
        Assert.Equal("m-1", loaded.ModelId);
        Assert.Equal(2, loaded.Messages.Count);
        Assert.Equal("line 1", loaded.Messages[0].Text);
        Assert.Equal("line 2", loaded.Messages[1].Text);
        Assert.Equal(ChatRole.Assistant, loaded.Messages[0].Role);
    }

    [Fact]
    public async Task SaveAsync_OverwritesExisting()
    {
        var store = new FileEvaluationCaptureStore(_directory);
        await store.SaveAsync(
            "key-x",
            new ChatResponse(new ChatMessage(ChatRole.Assistant, "first")),
            _ct);

        await store.SaveAsync(
            "key-x",
            new ChatResponse(new ChatMessage(ChatRole.Assistant, "second")),
            _ct);

        var loaded = await store.TryGetAsync("key-x", _ct);
        Assert.NotNull(loaded);
        Assert.Equal("second", loaded!.Messages[0].Text);
    }

    [Fact]
    public async Task SaveAsync_CreatesDirectoryOnFirstWrite()
    {
        Assert.False(Directory.Exists(_directory));
        var store = new FileEvaluationCaptureStore(_directory);

        await store.SaveAsync(
            "key",
            new ChatResponse(new ChatMessage(ChatRole.Assistant, "x")),
            _ct);

        Assert.True(Directory.Exists(_directory));
    }

    [Fact]
    public async Task SaveAsync_NullResponse_ThrowsArgumentNullException()
    {
        var store = new FileEvaluationCaptureStore(_directory);

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => store.SaveAsync("key", response: null!, _ct));
    }

    [Fact]
    public async Task TryGetAsync_NullOrWhitespaceKey_Throws()
    {
        var store = new FileEvaluationCaptureStore(_directory);

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => store.TryGetAsync(key: null!, _ct));
        await Assert.ThrowsAsync<ArgumentException>(
            () => store.TryGetAsync(key: "", _ct));
    }
}
