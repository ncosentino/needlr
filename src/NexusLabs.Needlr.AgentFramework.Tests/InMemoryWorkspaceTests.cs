using NexusLabs.Needlr.AgentFramework.Workspace;

namespace NexusLabs.Needlr.AgentFramework.Tests;

public class InMemoryWorkspaceTests
{
    // -------------------------------------------------------------------------
    // WriteFile + ReadFile
    // -------------------------------------------------------------------------

    [Fact]
    public void WriteFile_ThenReadFile_ReturnsContent()
    {
        var ws = new InMemoryWorkspace();
        ws.WriteFile("test.txt", "hello world");

        Assert.Equal("hello world", ws.ReadFile("test.txt"));
    }

    [Fact]
    public void WriteFile_Overwrites_ExistingContent()
    {
        var ws = new InMemoryWorkspace();
        ws.WriteFile("test.txt", "v1");
        ws.WriteFile("test.txt", "v2");

        Assert.Equal("v2", ws.ReadFile("test.txt"));
    }

    [Fact]
    public void ReadFile_NonExistent_ThrowsFileNotFound()
    {
        var ws = new InMemoryWorkspace();

        Assert.Throws<FileNotFoundException>(() => ws.ReadFile("missing.txt"));
    }

    // -------------------------------------------------------------------------
    // FileExists
    // -------------------------------------------------------------------------

    [Fact]
    public void FileExists_ExistingFile_ReturnsTrue()
    {
        var ws = new InMemoryWorkspace();
        ws.WriteFile("test.txt", "content");

        Assert.True(ws.FileExists("test.txt"));
    }

    [Fact]
    public void FileExists_NonExistent_ReturnsFalse()
    {
        var ws = new InMemoryWorkspace();

        Assert.False(ws.FileExists("missing.txt"));
    }

    // -------------------------------------------------------------------------
    // GetFilePaths
    // -------------------------------------------------------------------------

    [Fact]
    public void GetFilePaths_ReturnsAllPaths()
    {
        var ws = new InMemoryWorkspace();
        ws.WriteFile("a.txt", "a");
        ws.WriteFile("b.txt", "b");
        ws.WriteFile("dir/c.txt", "c");

        var paths = ws.GetFilePaths().OrderBy(p => p).ToList();

        Assert.Equal(3, paths.Count);
        Assert.Contains("a.txt", paths);
        Assert.Contains("b.txt", paths);
        Assert.Contains("dir/c.txt", paths);
    }

    [Fact]
    public void GetFilePaths_EmptyWorkspace_ReturnsEmpty()
    {
        var ws = new InMemoryWorkspace();

        Assert.Empty(ws.GetFilePaths());
    }

    // -------------------------------------------------------------------------
    // Path normalization
    // -------------------------------------------------------------------------

    [Fact]
    public void Paths_AreNormalized_BackslashToForward()
    {
        var ws = new InMemoryWorkspace();
        ws.WriteFile(@"dir\sub\file.txt", "content");

        Assert.True(ws.FileExists("dir/sub/file.txt"));
        Assert.Equal("content", ws.ReadFile("dir/sub/file.txt"));
    }

    [Fact]
    public void Paths_LeadingSlash_IsTrimmed()
    {
        var ws = new InMemoryWorkspace();
        ws.WriteFile("/root/file.txt", "content");

        Assert.True(ws.FileExists("root/file.txt"));
    }

    [Fact]
    public void Paths_AreCaseInsensitive()
    {
        var ws = new InMemoryWorkspace();
        ws.WriteFile("File.TXT", "content");

        Assert.True(ws.FileExists("file.txt"));
        Assert.Equal("content", ws.ReadFile("FILE.TXT"));
    }

    // -------------------------------------------------------------------------
    // CompareExchange
    // -------------------------------------------------------------------------

    [Fact]
    public void CompareExchange_MatchingContent_SwapsAndReturnsTrue()
    {
        var ws = new InMemoryWorkspace();
        ws.WriteFile("doc.md", "version 1");

        var result = ws.CompareExchange("doc.md", "version 1", "version 2");

        Assert.True(result);
        Assert.Equal("version 2", ws.ReadFile("doc.md"));
    }

    [Fact]
    public void CompareExchange_MismatchedContent_ReturnsFalse()
    {
        var ws = new InMemoryWorkspace();
        ws.WriteFile("doc.md", "version 1");

        var result = ws.CompareExchange("doc.md", "wrong content", "version 2");

        Assert.False(result);
        Assert.Equal("version 1", ws.ReadFile("doc.md"));
    }

    [Fact]
    public void CompareExchange_NonExistentFile_ReturnsFalse()
    {
        var ws = new InMemoryWorkspace();

        var result = ws.CompareExchange("missing.md", "any", "new");

        Assert.False(result);
    }

    // -------------------------------------------------------------------------
    // SeedFile
    // -------------------------------------------------------------------------

    [Fact]
    public void SeedFile_CreatesFile()
    {
        var ws = new InMemoryWorkspace();
        ws.SeedFile("setup.txt", "seeded content");

        Assert.True(ws.FileExists("setup.txt"));
        Assert.Equal("seeded content", ws.ReadFile("setup.txt"));
    }

    // -------------------------------------------------------------------------
    // Thread safety
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ConcurrentWrites_AllSucceed()
    {
        var ws = new InMemoryWorkspace();
        var ct = TestContext.Current.CancellationToken;

        var tasks = Enumerable.Range(0, 100).Select(i =>
            Task.Run(() => ws.WriteFile($"file-{i}.txt", $"content-{i}"), ct));

        await Task.WhenAll(tasks);

        Assert.Equal(100, ws.GetFilePaths().Count());
    }

    // -------------------------------------------------------------------------
    // ReadFileAsMemory
    // -------------------------------------------------------------------------

    [Fact]
    public void ReadFileAsMemory_ExistingFile_ReturnsContent()
    {
        var ws = new InMemoryWorkspace();
        ws.WriteFile("test.md", "# Hello\n\nWorld");

        var memory = ws.ReadFileAsMemory("test.md");

        Assert.Equal("# Hello\n\nWorld", memory.ToString());
    }

    [Fact]
    public void ReadFileAsMemory_MissingFile_Throws()
    {
        var ws = new InMemoryWorkspace();

        Assert.Throws<FileNotFoundException>(() => ws.ReadFileAsMemory("nope.txt"));
    }

    [Fact]
    public void ReadFileAsMemory_CanEnumerateLines()
    {
        var ws = new InMemoryWorkspace();
        ws.WriteFile("lines.txt", "line1\nline2\nline3");

        var memory = ws.ReadFileAsMemory("lines.txt");
        var lineCount = 0;
        foreach (var _ in memory.Span.EnumerateLines())
            lineCount++;

        Assert.Equal(3, lineCount);
    }

    // -------------------------------------------------------------------------
    // ListDirectory
    // -------------------------------------------------------------------------

    [Fact]
    public void ListDirectory_EmptyWorkspace_ReturnsRoot()
    {
        var ws = new InMemoryWorkspace();

        var result = ws.ListDirectory("");

        Assert.Equal("./", result);
    }

    [Fact]
    public void ListDirectory_FlatFiles_ListsAll()
    {
        var ws = new InMemoryWorkspace();
        ws.SeedFile("a.txt", "");
        ws.SeedFile("b.txt", "");

        var result = ws.ListDirectory("");

        Assert.Contains("a.txt", result);
        Assert.Contains("b.txt", result);
    }

    [Fact]
    public void ListDirectory_NestedFiles_ShowsTree()
    {
        var ws = new InMemoryWorkspace();
        ws.SeedFile("src/main.cs", "");
        ws.SeedFile("src/util/helper.cs", "");
        ws.SeedFile("readme.md", "");

        var result = ws.ListDirectory("");

        Assert.Contains("src/", result);
        Assert.Contains("readme.md", result);
        Assert.Contains("main.cs", result);
    }

    [Fact]
    public void ListDirectory_MaxDepth_LimitsTraversal()
    {
        var ws = new InMemoryWorkspace();
        ws.SeedFile("a/b/c/deep.txt", "");
        ws.SeedFile("a/top.txt", "");

        var result = ws.ListDirectory("", maxDepth: 1);

        Assert.Contains("a/", result);
        // At depth 1, we see the 'a' directory but not its children
        Assert.DoesNotContain("deep.txt", result);
        Assert.DoesNotContain("top.txt", result);
    }

    [Fact]
    public void ListDirectory_Subdirectory_ScopedToPrefix()
    {
        var ws = new InMemoryWorkspace();
        ws.SeedFile("src/main.cs", "");
        ws.SeedFile("src/util/helper.cs", "");
        ws.SeedFile("docs/readme.md", "");

        var result = ws.ListDirectory("src");

        Assert.Contains("main.cs", result);
        Assert.Contains("util/", result);
        Assert.DoesNotContain("readme.md", result);
    }
}
