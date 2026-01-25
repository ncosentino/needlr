using Xunit;

using static NexusLabs.Needlr.Generators.Tests.Diagnostics.DiagnosticTestHelpers;

namespace NexusLabs.Needlr.Generators.Tests.Diagnostics;

public sealed class DiagnosticMSBuildPropertyTests
{
    [Theory]
    [InlineData("true")]
    [InlineData("True")]
    [InlineData("TRUE")]
    [InlineData("1")]
    public void DiagnosticsEnabled_VariousFormats_AllWork(string enabledValue)
    {
        var source = @"
using NexusLabs.Needlr.Generators;

[assembly: GenerateTypeRegistry]

namespace TestApp
{
    public interface IMyService { }
    public class MyService : IMyService { }
}";

        var generatedFiles = RunGeneratorWithDiagnosticsEnabledValue(source, enabledValue);

        Assert.Contains(generatedFiles, f => f.FilePath.Contains("NeedlrDiagnostics"));
    }

    [Theory]
    [InlineData("false")]
    [InlineData("False")]
    [InlineData("FALSE")]
    [InlineData("0")]
    [InlineData("")]
    [InlineData("invalid")]
    public void DiagnosticsDisabled_VariousFormats_AllWork(string enabledValue)
    {
        var source = @"
using NexusLabs.Needlr.Generators;

[assembly: GenerateTypeRegistry]

namespace TestApp
{
    public interface IMyService { }
    public class MyService : IMyService { }
}";

        var generatedFiles = RunGeneratorWithDiagnosticsEnabledValue(source, enabledValue);

        Assert.DoesNotContain(generatedFiles, f => f.FilePath.Contains("NeedlrDiagnostics"));
    }
}
