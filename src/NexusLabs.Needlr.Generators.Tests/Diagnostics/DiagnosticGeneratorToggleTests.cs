using Xunit;

using static NexusLabs.Needlr.Generators.Tests.Diagnostics.DiagnosticTestHelpers;

namespace NexusLabs.Needlr.Generators.Tests.Diagnostics;

public sealed class DiagnosticGeneratorToggleTests
{
    [Fact]
    public void Generator_DiagnosticsEnabled_GeneratesDiagnosticsFile()
    {
        var source = @"
using NexusLabs.Needlr.Generators;

[assembly: GenerateTypeRegistry]

namespace TestApp
{
    public interface IMyService { }
    public class MyService : IMyService { }
}";

        var generatedFiles = RunGeneratorWithDiagnosticsEnabled(source, enabled: true);

        Assert.Contains(generatedFiles, f => f.FilePath.Contains("NeedlrDiagnostics"));
    }

    [Fact]
    public void Generator_DiagnosticsDisabled_DoesNotGenerateDiagnosticsFile()
    {
        var source = @"
using NexusLabs.Needlr.Generators;

[assembly: GenerateTypeRegistry]

namespace TestApp
{
    public interface IMyService { }
    public class MyService : IMyService { }
}";

        var generatedFiles = RunGeneratorWithDiagnosticsEnabled(source, enabled: false);

        Assert.DoesNotContain(generatedFiles, f => f.FilePath.Contains("NeedlrDiagnostics"));
    }

    [Fact]
    public void Generator_DiagnosticsNotSet_DoesNotGenerateDiagnosticsFile()
    {
        var source = @"
using NexusLabs.Needlr.Generators;

[assembly: GenerateTypeRegistry]

namespace TestApp
{
    public interface IMyService { }
    public class MyService : IMyService { }
}";

        var generatedFiles = RunGeneratorWithDiagnosticsEnabled(source, enabled: null);

        Assert.DoesNotContain(generatedFiles, f => f.FilePath.Contains("NeedlrDiagnostics"));
    }
}
