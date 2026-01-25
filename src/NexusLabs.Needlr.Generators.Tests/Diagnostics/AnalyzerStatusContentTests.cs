using Xunit;

using static NexusLabs.Needlr.Generators.Tests.Diagnostics.DiagnosticTestHelpers;

namespace NexusLabs.Needlr.Generators.Tests.Diagnostics;

public sealed class AnalyzerStatusContentTests
{
    [Fact]
    public void AnalyzerStatus_ContainsHeader()
    {
        var source = @"
using NexusLabs.Needlr.Generators;

[assembly: GenerateTypeRegistry]

namespace TestApp
{
    public interface IMyService { }
    public class MyService : IMyService { }
}";

        var content = GetDiagnosticContent(source, "AnalyzerStatus");

        Assert.Contains("# Needlr Analyzer Status", content);
    }

    [Fact]
    public void AnalyzerStatus_ContainsAnalyzerTable()
    {
        var source = @"
using NexusLabs.Needlr.Generators;

[assembly: GenerateTypeRegistry]

namespace TestApp
{
    public interface IMyService { }
    public class MyService : IMyService { }
}";

        var content = GetDiagnosticContent(source, "AnalyzerStatus");

        Assert.Contains("| ID | Name | Status | Default Severity | Description |", content);
        Assert.Contains("NDLRCOR001", content);
        Assert.Contains("NDLRCOR009", content);
        Assert.Contains("NDLRCOR010", content);
    }

    [Fact]
    public void AnalyzerStatus_ShowsLazyResolutionAnalyzer()
    {
        var source = @"
using NexusLabs.Needlr.Generators;

[assembly: GenerateTypeRegistry]

namespace TestApp
{
    public interface IMyService { }
    public class MyService : IMyService { }
}";

        var content = GetDiagnosticContent(source, "AnalyzerStatus");

        Assert.Contains("NDLRCOR009", content);
        Assert.Contains("Lazy Resolution", content);
        Assert.Contains("Info", content);
    }

    [Fact]
    public void AnalyzerStatus_ShowsCollectionResolutionAnalyzer()
    {
        var source = @"
using NexusLabs.Needlr.Generators;

[assembly: GenerateTypeRegistry]

namespace TestApp
{
    public interface IMyService { }
    public class MyService : IMyService { }
}";

        var content = GetDiagnosticContent(source, "AnalyzerStatus");

        Assert.Contains("NDLRCOR010", content);
        Assert.Contains("Collection Resolution", content);
    }

    [Fact]
    public void AnalyzerStatus_ContainsSourceGenMode()
    {
        var source = @"
using NexusLabs.Needlr.Generators;

[assembly: GenerateTypeRegistry]

namespace TestApp
{
    public interface IMyService { }
    public class MyService : IMyService { }
}";

        var content = GetDiagnosticContent(source, "AnalyzerStatus");

        Assert.Contains("Source Generation", content);
        Assert.Contains("GenerateTypeRegistry detected", content);
    }

    [Fact]
    public void AnalyzerStatus_ContainsEditorConfigExample()
    {
        var source = @"
using NexusLabs.Needlr.Generators;

[assembly: GenerateTypeRegistry]

namespace TestApp
{
    public interface IMyService { }
    public class MyService : IMyService { }
}";

        var content = GetDiagnosticContent(source, "AnalyzerStatus");

        Assert.Contains(".editorconfig", content);
        Assert.Contains("dotnet_diagnostic.NDLRCOR009.severity", content);
    }
}
