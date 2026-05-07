using NexusLabs.Needlr.AgentFramework.Workspace;

namespace NexusLabs.Needlr.AgentFramework.Tests.Workspace;

public class WorkspacePathTests
{
    public class CanonicalizeMethod
    {
        [Fact]
        public void NullInput_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => WorkspacePath.Canonicalize(null!));
        }

        [Theory]
        [InlineData("")]
        [InlineData(" ")]
        [InlineData("\t")]
        [InlineData("   ")]
        [InlineData("\r\n")]
        public void EmptyOrWhitespace_ThrowsArgumentException(string input)
        {
            Assert.Throws<ArgumentException>(() => WorkspacePath.Canonicalize(input));
        }

        [Theory]
        [InlineData("/")]
        [InlineData("//")]
        [InlineData("///")]
        [InlineData(".")]
        [InlineData("./")]
        [InlineData("/.")]
        [InlineData("/./")]
        [InlineData("./.")]
        [InlineData("././.")]
        [InlineData("\\")]
        [InlineData("  /  ")]
        public void CanonicalizesToEmpty_ThrowsArgumentException(string input)
        {
            Assert.Throws<ArgumentException>(() => WorkspacePath.Canonicalize(input));
        }

        [Theory]
        [InlineData("..")]
        [InlineData("../foo")]
        [InlineData("foo/..")]
        [InlineData("foo/../bar")]
        [InlineData("foo/bar/..")]
        [InlineData("../../foo")]
        [InlineData("foo/./../bar")]
        [InlineData(@"foo\..\bar")]
        public void ContainsParentSegment_ThrowsArgumentException(string input)
        {
            Assert.Throws<ArgumentException>(() => WorkspacePath.Canonicalize(input));
        }

        [Theory]
        [InlineData("version..draft.md")]
        [InlineData("foo..bar.md")]
        [InlineData("...folder/file.md")]
        [InlineData("kb/version..draft.md")]
        [InlineData("..hidden")]
        [InlineData("hidden..")]
        public void DoubleDotInSegmentName_IsAccepted(string input)
        {
            var result = WorkspacePath.Canonicalize(input);
            Assert.NotEmpty(result);
        }

        [Theory]
        [InlineData("kb/foo.md", "kb/foo.md")]
        [InlineData("./kb/foo.md", "kb/foo.md")]
        [InlineData("kb//foo.md", "kb/foo.md")]
        [InlineData("kb/./foo.md", "kb/foo.md")]
        [InlineData("/kb/foo.md", "kb/foo.md")]
        [InlineData("kb/foo.md/", "kb/foo.md")]
        [InlineData("/kb/foo.md/", "kb/foo.md")]
        [InlineData("//kb//foo.md//", "kb/foo.md")]
        [InlineData(@"kb\foo.md", "kb/foo.md")]
        [InlineData(@"kb\sub\file.txt", "kb/sub/file.txt")]
        [InlineData(@".\kb\foo.md", "kb/foo.md")]
        [InlineData("/foo/./bar//baz/", "foo/bar/baz")]
        public void StructuralCanonicalization_ProducesExpectedForm(string input, string expected)
        {
            Assert.Equal(expected, WorkspacePath.Canonicalize(input));
        }

        [Theory]
        [InlineData("  kb/foo.md  ", "kb/foo.md")]
        [InlineData("\tkb/foo.md\t", "kb/foo.md")]
        [InlineData("\r\nkb/foo.md\r\n", "kb/foo.md")]
        public void OuterWhitespace_IsTrimmed(string input, string expected)
        {
            Assert.Equal(expected, WorkspacePath.Canonicalize(input));
        }

        [Theory]
        [InlineData("foo /bar.md", "foo /bar.md")]
        [InlineData("foo/ bar.md", "foo/ bar.md")]
        [InlineData("My Documents/file.md", "My Documents/file.md")]
        [InlineData("kb/file with spaces.md", "kb/file with spaces.md")]
        public void SegmentInternalWhitespace_IsPreserved(string input, string expected)
        {
            Assert.Equal(expected, WorkspacePath.Canonicalize(input));
        }

        [Theory]
        [InlineData("Kb/Foo.md")]
        [InlineData("KB/FOO.MD")]
        [InlineData("kb/foo.md")]
        public void Casing_IsPreserved(string input)
        {
            Assert.Equal(input, WorkspacePath.Canonicalize(input));
        }
    }

    public class CanonicalizeDirectoryMethod
    {
        [Fact]
        public void NullInput_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => WorkspacePath.CanonicalizeDirectory(null!));
        }

        [Theory]
        [InlineData("")]
        [InlineData(" ")]
        [InlineData("\t")]
        [InlineData("   ")]
        [InlineData(".")]
        [InlineData("./")]
        [InlineData("/")]
        [InlineData("//")]
        [InlineData("///")]
        [InlineData("/./")]
        [InlineData("/.")]
        [InlineData("./.")]
        [InlineData("\\")]
        [InlineData("  /  ")]
        public void RootEquivalentInputs_ReturnEmpty(string input)
        {
            Assert.Equal(string.Empty, WorkspacePath.CanonicalizeDirectory(input));
        }

        [Theory]
        [InlineData("..")]
        [InlineData("../src")]
        [InlineData("src/..")]
        [InlineData("src/../docs")]
        public void ContainsParentSegment_ThrowsArgumentException(string input)
        {
            Assert.Throws<ArgumentException>(() => WorkspacePath.CanonicalizeDirectory(input));
        }

        [Theory]
        [InlineData("src", "src")]
        [InlineData("./src", "src")]
        [InlineData("src/", "src")]
        [InlineData("/src", "src")]
        [InlineData("src//", "src")]
        [InlineData("/src/", "src")]
        [InlineData("src/util", "src/util")]
        [InlineData("./src//util/", "src/util")]
        [InlineData(@"src\util", "src/util")]
        public void NonRootDirectories_AreCanonicalized(string input, string expected)
        {
            Assert.Equal(expected, WorkspacePath.CanonicalizeDirectory(input));
        }
    }

    public class PathComparerProperty
    {
        [Fact]
        public void IsOrdinalIgnoreCase()
        {
            Assert.Same(StringComparer.OrdinalIgnoreCase, WorkspacePath.PathComparer);
        }

        [Theory]
        [InlineData("kb/foo.md", "KB/FOO.MD")]
        [InlineData("kb/Foo.md", "kb/foo.md")]
        [InlineData("Kb/FOO.md", "kB/foo.MD")]
        public void Equals_TreatsCasingAsEquivalent(string a, string b)
        {
            Assert.True(WorkspacePath.PathComparer.Equals(a, b));
        }

        [Theory]
        [InlineData("kb/foo.md", "kb/bar.md")]
        [InlineData("kb/foo.md", "kb/foo")]
        [InlineData("kb/foo.md", "docs/foo.md")]
        public void Equals_DistinguishesDifferentPaths(string a, string b)
        {
            Assert.False(WorkspacePath.PathComparer.Equals(a, b));
        }
    }
}
