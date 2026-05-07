using NexusLabs.Needlr.AgentFramework.Workspace;

namespace NexusLabs.Needlr.AgentFramework.Tests;

public class InMemoryWorkspaceTests
{
    [Fact]
    public void WriteFile_ThenReadFile_ReturnsContent()
    {
        var ws = new InMemoryWorkspace();
        ws.SeedFile("test.txt", "hello world");

        Assert.Equal("hello world", ws.TryReadFile("test.txt").Value.Content);
    }

    [Fact]
    public void WriteFile_Overwrites_ExistingContent()
    {
        var ws = new InMemoryWorkspace();
        ws.SeedFile("test.txt", "v1");
        ws.TryWriteFile("test.txt", "v2");

        Assert.Equal("v2", ws.TryReadFile("test.txt").Value.Content);
    }

    [Fact]
    public void ReadFile_NonExistent_ThrowsFileNotFound()
    {
        var ws = new InMemoryWorkspace();

        var result = ws.TryReadFile("missing.txt");
        Assert.False(result.Success);
        Assert.IsType<FileNotFoundException>(result.Exception);
    }

    [Fact]
    public void FileExists_ExistingFile_ReturnsTrue()
    {
        var ws = new InMemoryWorkspace();
        ws.SeedFile("test.txt", "content");

        Assert.True(ws.FileExists("test.txt"));
    }

    [Fact]
    public void FileExists_NonExistent_ReturnsFalse()
    {
        var ws = new InMemoryWorkspace();

        Assert.False(ws.FileExists("missing.txt"));
    }

    [Fact]
    public void GetFilePaths_ReturnsAllPaths()
    {
        var ws = new InMemoryWorkspace();
        ws.SeedFile("a.txt", "a");
        ws.SeedFile("b.txt", "b");
        ws.SeedFile("dir/c.txt", "c");

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

    [Fact]
    public void GetFilePaths_AfterAliasingWrites_ReturnsCanonicalKeys()
    {
        var ws = new InMemoryWorkspace();
        ws.TryWriteFile("kb/foo.md", "v1");
        ws.TryWriteFile("./kb/foo.md", "v2");
        ws.TryWriteFile("kb//foo.md", "v3");
        ws.TryWriteFile("/kb/foo.md", "v4");
        ws.TryWriteFile("kb/foo.md/", "v5");

        var paths = ws.GetFilePaths().ToList();

        Assert.Single(paths);
        Assert.Equal("kb/foo.md", paths[0]);
    }

    [Fact]
    public void Paths_AreNormalized_BackslashToForward()
    {
        var ws = new InMemoryWorkspace();
        ws.SeedFile(@"dir\sub\file.txt", "content");

        Assert.True(ws.FileExists("dir/sub/file.txt"));
        Assert.Equal("content", ws.TryReadFile("dir/sub/file.txt").Value.Content);
    }

    [Fact]
    public void Paths_LeadingSlash_IsTrimmed()
    {
        var ws = new InMemoryWorkspace();
        ws.SeedFile("/root/file.txt", "content");

        Assert.True(ws.FileExists("root/file.txt"));
    }

    [Fact]
    public void Paths_TrailingSlash_IsTrimmed()
    {
        var ws = new InMemoryWorkspace();
        ws.SeedFile("kb/foo.md/", "content");

        Assert.True(ws.FileExists("kb/foo.md"));
    }

    [Fact]
    public void Paths_AreCaseInsensitive()
    {
        var ws = new InMemoryWorkspace();
        ws.SeedFile("File.TXT", "content");

        Assert.True(ws.FileExists("file.txt"));
        Assert.Equal("content", ws.TryReadFile("FILE.TXT").Value.Content);
    }

    [Fact]
    public void AliasingPaths_AreTreatedAsTheSameFile()
    {
        var ws = new InMemoryWorkspace();
        ws.TryWriteFile("kb/foo.md", "content-A");
        ws.TryWriteFile("./kb/foo.md", "content-B");

        Assert.Single(ws.GetFilePaths());
        Assert.Equal("content-B", ws.TryReadFile("kb/foo.md").Value.Content);
    }

    [Fact]
    public void RedundantSeparators_CollapseToSingleEntry()
    {
        var ws = new InMemoryWorkspace();
        ws.TryWriteFile("kb/audience/foo.md", "v1");
        ws.TryWriteFile("kb//audience/foo.md", "v2");
        ws.TryWriteFile("kb/./audience/foo.md", "v3");
        ws.TryWriteFile("/kb/audience/foo.md", "v4");

        Assert.Single(ws.GetFilePaths());
        Assert.Equal("v4", ws.TryReadFile("kb/audience/foo.md").Value.Content);
    }

    [Fact]
    public void CompareExchange_CanonicalizesBeforeMatching()
    {
        var ws = new InMemoryWorkspace();
        ws.TryWriteFile("kb/foo.md", "v1");

        var result = ws.TryCompareExchange("./kb/foo.md", "v1", "v2");

        Assert.True(result.Success);
        Assert.True(result.Value.Exchanged);
        Assert.Equal("v2", ws.TryReadFile("kb/foo.md").Value.Content);
    }

    [Fact]
    public void TryReadFile_ActualPath_IsCanonical()
    {
        var ws = new InMemoryWorkspace();
        ws.SeedFile("kb/foo.md", "content");

        var result = ws.TryReadFile("./kb//foo.md/");

        Assert.True(result.Success);
        Assert.Equal("kb/foo.md", result.Value.ActualPath);
    }

    [Fact]
    public void TryWriteFile_ActualPath_IsCanonical()
    {
        var ws = new InMemoryWorkspace();

        var result = ws.TryWriteFile(@"./kb\foo.md/", "content");

        Assert.True(result.Success);
        Assert.Equal("kb/foo.md", result.Value.ActualPath);
    }

    [Fact]
    public void CompareExchange_MatchingContent_SwapsAndReturnsTrue()
    {
        var ws = new InMemoryWorkspace();
        ws.SeedFile("doc.md", "version 1");

        var result = ws.TryCompareExchange("doc.md", "version 1", "version 2");

        Assert.True(result.Success);
        Assert.True(result.Value.Exchanged);
        Assert.Equal("version 2", ws.TryReadFile("doc.md").Value.Content);
    }

    [Fact]
    public void CompareExchange_MismatchedContent_ReturnsFalse()
    {
        var ws = new InMemoryWorkspace();
        ws.SeedFile("doc.md", "version 1");

        var result = ws.TryCompareExchange("doc.md", "wrong content", "version 2");

        Assert.True(result.Success);
        Assert.False(result.Value.Exchanged);
        Assert.Equal("version 1", ws.TryReadFile("doc.md").Value.Content);
    }

    [Fact]
    public void TryCompareExchange_NonExistentFile_ReturnsFail()
    {
        var ws = new InMemoryWorkspace();

        var result = ws.TryCompareExchange("missing.md", "any", "new");

        Assert.False(result.Success);
        Assert.IsType<FileNotFoundException>(result.Exception);
    }

    [Fact]
    public void SeedFile_CreatesFile()
    {
        var ws = new InMemoryWorkspace();
        ws.SeedFile("setup.txt", "seeded content");

        Assert.True(ws.FileExists("setup.txt"));
        Assert.Equal("seeded content", ws.TryReadFile("setup.txt").Value.Content);
    }

    [Fact]
    public async Task ConcurrentWrites_AllSucceed()
    {
        var ws = new InMemoryWorkspace();
        var ct = TestContext.Current.CancellationToken;

        var tasks = Enumerable.Range(0, 100).Select(i =>
            Task.Run(() => ws.TryWriteFile($"file-{i}.txt", $"content-{i}"), ct));

        await Task.WhenAll(tasks);

        Assert.Equal(100, ws.GetFilePaths().Count());
    }

    [Fact]
    public void ReadFileAsMemory_ExistingFile_ReturnsContent()
    {
        var ws = new InMemoryWorkspace();
        ws.SeedFile("test.md", "# Hello\n\nWorld");

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
        ws.SeedFile("lines.txt", "line1\nline2\nline3");

        var memory = ws.ReadFileAsMemory("lines.txt");
        var lineCount = 0;
        foreach (var _ in memory.Span.EnumerateLines())
            lineCount++;

        Assert.Equal(3, lineCount);
    }

    [Fact]
    public void ListDirectory_EmptyWorkspace_ReturnsRoot()
    {
        var ws = new InMemoryWorkspace();

        var result = ws.ListDirectory("");

        Assert.Equal("./", result);
    }

    [Theory]
    [InlineData("")]
    [InlineData(".")]
    [InlineData("./")]
    [InlineData("/")]
    [InlineData("//")]
    [InlineData("/./")]
    [InlineData(" ")]
    public void ListDirectory_RootSentinels_ListRoot(string sentinel)
    {
        var ws = new InMemoryWorkspace();
        ws.SeedFile("a.txt", "");
        ws.SeedFile("b.txt", "");

        var result = ws.ListDirectory(sentinel);

        Assert.Contains("a.txt", result);
        Assert.Contains("b.txt", result);
    }

    [Theory]
    [InlineData("src")]
    [InlineData("./src")]
    [InlineData("src/")]
    [InlineData("/src")]
    [InlineData("src//")]
    [InlineData(@"src\")]
    public void ListDirectory_DirectoryAliases_ScopeToSamePrefix(string variant)
    {
        var ws = new InMemoryWorkspace();
        ws.SeedFile("src/main.cs", "");
        ws.SeedFile("src/util/helper.cs", "");
        ws.SeedFile("docs/readme.md", "");

        var result = ws.ListDirectory(variant);

        Assert.Contains("main.cs", result);
        Assert.Contains("util/", result);
        Assert.DoesNotContain("readme.md", result);
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

    [Fact]
    public void TryReadFile_ParentSegment_ThrowsArgumentException()
    {
        var ws = new InMemoryWorkspace();

        Assert.Throws<ArgumentException>(() => ws.TryReadFile("../foo.md"));
        Assert.Throws<ArgumentException>(() => ws.TryReadFile("kb/../foo.md"));
    }

    [Fact]
    public void TryWriteFile_ParentSegment_ThrowsArgumentException()
    {
        var ws = new InMemoryWorkspace();

        Assert.Throws<ArgumentException>(() => ws.TryWriteFile("../foo.md", "content"));
        Assert.Throws<ArgumentException>(() => ws.TryWriteFile("kb/../foo.md", "content"));
    }

    [Fact]
    public void FileExists_ParentSegment_ThrowsArgumentException()
    {
        var ws = new InMemoryWorkspace();

        Assert.Throws<ArgumentException>(() => ws.FileExists("../foo.md"));
    }

    [Fact]
    public void TryCompareExchange_ParentSegment_ThrowsArgumentException()
    {
        var ws = new InMemoryWorkspace();

        Assert.Throws<ArgumentException>(() => ws.TryCompareExchange("../foo.md", "a", "b"));
    }

    [Fact]
    public void ReadFileAsMemory_ParentSegment_ThrowsArgumentException()
    {
        var ws = new InMemoryWorkspace();

        Assert.Throws<ArgumentException>(() => ws.ReadFileAsMemory("../foo.md"));
    }

    [Fact]
    public void ListDirectory_ParentSegment_ThrowsArgumentException()
    {
        var ws = new InMemoryWorkspace();

        Assert.Throws<ArgumentException>(() => ws.ListDirectory("../src"));
    }

    [Fact]
    public void SeedFile_ParentSegment_ThrowsArgumentException()
    {
        var ws = new InMemoryWorkspace();

        Assert.Throws<ArgumentException>(() => ws.SeedFile("../foo.md", "content"));
    }

    [Fact]
    public void TryReadFile_NullPath_ThrowsArgumentNullException()
    {
        var ws = new InMemoryWorkspace();

        Assert.Throws<ArgumentNullException>(() => ws.TryReadFile(null!));
    }

    [Fact]
    public void TryWriteFile_EmptyPath_ThrowsArgumentException()
    {
        var ws = new InMemoryWorkspace();

        Assert.Throws<ArgumentException>(() => ws.TryWriteFile("", "content"));
    }

    [Fact]
    public void TryWriteFile_RootEquivalentPath_ThrowsArgumentException()
    {
        var ws = new InMemoryWorkspace();

        Assert.Throws<ArgumentException>(() => ws.TryWriteFile("/", "content"));
        Assert.Throws<ArgumentException>(() => ws.TryWriteFile(".", "content"));
        Assert.Throws<ArgumentException>(() => ws.TryWriteFile("./", "content"));
    }
}
