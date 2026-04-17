using NexusLabs.Needlr.AgentFramework.Workspace;

namespace NexusLabs.Needlr.AgentFramework.Tests;

public sealed class WorkspaceResultTests
{
    [Fact]
    public void TryReadFile_ExistingFile_ReturnsSuccess()
    {
        var ws = new InMemoryWorkspace();
        ws.SeedFile("test.txt", "hello world");

        var result = ws.TryReadFile("test.txt");

        Assert.True(result.Success);
        Assert.Equal("hello world", result.Value.Content);
        Assert.Equal("test.txt", result.Value.ActualPath);
        Assert.Null(result.Exception);
    }

    [Fact]
    public void TryReadFile_MissingFile_ReturnsFail()
    {
        var ws = new InMemoryWorkspace();

        var result = ws.TryReadFile("missing.txt");

        Assert.False(result.Success);
        Assert.IsType<FileNotFoundException>(result.Exception);
    }

    [Fact]
    public void TryWriteFile_NewFile_ReturnsSuccess()
    {
        var ws = new InMemoryWorkspace();

        var result = ws.TryWriteFile("new.txt", "content");

        Assert.True(result.Success);
        Assert.Equal(7, result.Value.CharsWritten);
        Assert.Equal("new.txt", result.Value.ActualPath);
        Assert.True(ws.FileExists("new.txt"));
    }

    [Fact]
    public void TryWriteFile_Overwrite_ReturnsSuccess()
    {
        var ws = new InMemoryWorkspace();
        ws.SeedFile("file.txt", "old");

        var result = ws.TryWriteFile("file.txt", "new content");

        Assert.True(result.Success);
        Assert.Equal(11, result.Value.CharsWritten);
        Assert.Equal("new content", ws.TryReadFile("file.txt").Value.Content);
    }

    [Fact]
    public void TryCompareExchange_MatchingContent_ReturnsExchangedTrue()
    {
        var ws = new InMemoryWorkspace();
        ws.SeedFile("doc.md", "version 1");

        var result = ws.TryCompareExchange("doc.md", "version 1", "version 2");

        Assert.True(result.Success);
        Assert.True(result.Value.Exchanged);
        Assert.Null(result.Value.Reason);
        Assert.Equal("version 2", ws.TryReadFile("doc.md").Value.Content);
    }

    [Fact]
    public void TryCompareExchange_MismatchContent_ReturnsExchangedFalse()
    {
        var ws = new InMemoryWorkspace();
        ws.SeedFile("doc.md", "version 1");

        var result = ws.TryCompareExchange("doc.md", "wrong content", "version 2");

        Assert.True(result.Success);
        Assert.False(result.Value.Exchanged);
        Assert.NotNull(result.Value.Reason);
        Assert.Equal("version 1", ws.TryReadFile("doc.md").Value.Content);
    }

    [Fact]
    public void TryCompareExchange_MissingFile_ReturnsFail()
    {
        var ws = new InMemoryWorkspace();

        var result = ws.TryCompareExchange("missing.md", "any", "new");

        Assert.False(result.Success);
        Assert.IsType<FileNotFoundException>(result.Exception);
    }

    [Fact]
    public void WorkspaceResult_Ok_CarriesValue()
    {
        var result = WorkspaceResult<WriteFileResult>.Ok(
            new WriteFileResult("test.txt", 42));

        Assert.True(result.Success);
        Assert.Equal("test.txt", result.Value.ActualPath);
        Assert.Equal(42, result.Value.CharsWritten);
        Assert.Null(result.Exception);
    }

    [Fact]
    public void WorkspaceResult_Fail_CarriesException()
    {
        var ex = new InvalidOperationException("quota exceeded");
        var result = WorkspaceResult<WriteFileResult>.Fail(ex);

        Assert.False(result.Success);
        Assert.Same(ex, result.Exception);
    }
}
