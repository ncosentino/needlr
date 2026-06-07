using System.Threading.Tasks;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

using Xunit;

namespace NexusLabs.Needlr.Logging.Analyzers.Tests;

public sealed class NeedlrLoggerMessageAnalyzerTests
{
    private const string Preamble = @"
namespace NexusLabs.Needlr.Logging
{
    [System.AttributeUsage(System.AttributeTargets.Method)]
    public sealed class NeedlrLoggerMessageAttribute : System.Attribute { }
}
namespace Microsoft.Extensions.Logging
{
    public interface ILogger { }
}
";

    [Fact]
    public async Task ValidUsage_ProducesNoDiagnostics()
    {
        var code = @"
using NexusLabs.Needlr.Logging;
using Microsoft.Extensions.Logging;

public partial class Target
{
    private ILogger _logger;

    [NeedlrLoggerMessage]
    partial void LogIt(int count, System.Exception ex);
}
" + Preamble;

        await RunAsync(code);
    }

    [Fact]
    public async Task NonPartialMethod_ReportsMustBePartial()
    {
        var code = @"
using NexusLabs.Needlr.Logging;
using Microsoft.Extensions.Logging;

public partial class Target
{
    private ILogger _logger;

    [NeedlrLoggerMessage]
    void {|#0:LogIt|}(int count, System.Exception ex) { }
}
" + Preamble;

        var expected = new DiagnosticResult(DiagnosticIds.MustBePartial, DiagnosticSeverity.Error)
            .WithLocation(0)
            .WithArguments("LogIt");

        await RunAsync(code, expected);
    }

    [Fact]
    public async Task GenericMethod_ReportsMustNotBeGeneric()
    {
        var code = @"
using NexusLabs.Needlr.Logging;
using Microsoft.Extensions.Logging;

public partial class Target
{
    private ILogger _logger;

    [NeedlrLoggerMessage]
    partial void {|#0:LogIt|}<T>(T value);
}
" + Preamble;

        var expected = new DiagnosticResult(DiagnosticIds.MustNotBeGeneric, DiagnosticSeverity.Error)
            .WithLocation(0)
            .WithArguments("LogIt");

        await RunAsync(code, expected);
    }

    [Fact]
    public async Task NonVoidMethod_ReportsMustReturnVoid()
    {
        var code = @"
using NexusLabs.Needlr.Logging;
using Microsoft.Extensions.Logging;

public partial class Target
{
    private ILogger _logger;

    [NeedlrLoggerMessage]
    public partial int {|#0:LogIt|}(System.Exception ex);
    public partial int LogIt(System.Exception ex) => 0;
}
" + Preamble;

        var expected = new DiagnosticResult(DiagnosticIds.MustReturnVoid, DiagnosticSeverity.Error)
            .WithLocation(0)
            .WithArguments("LogIt");

        await RunAsync(code, expected);
    }

    [Fact]
    public async Task NonPartialContainingType_ReportsContainingTypeAndMustBePartial()
    {
        var code = @"
using NexusLabs.Needlr.Logging;
using Microsoft.Extensions.Logging;

public class Target
{
    private ILogger _logger;

    [NeedlrLoggerMessage]
    void {|#0:LogIt|}(System.Exception ex) { }
}
" + Preamble;

        var mustBePartial = new DiagnosticResult(DiagnosticIds.MustBePartial, DiagnosticSeverity.Error)
            .WithLocation(0)
            .WithArguments("LogIt");
        var containingType = new DiagnosticResult(DiagnosticIds.ContainingTypeMustBePartial, DiagnosticSeverity.Error)
            .WithLocation(0)
            .WithArguments("Target");

        await RunAsync(code, mustBePartial, containingType);
    }

    [Fact]
    public async Task NoLogger_ReportsLoggerNotFound()
    {
        var code = @"
using NexusLabs.Needlr.Logging;

public partial class Target
{
    [NeedlrLoggerMessage]
    partial void {|#0:LogIt|}(System.Exception ex);
}
" + Preamble;

        var expected = new DiagnosticResult(DiagnosticIds.LoggerNotFound, DiagnosticSeverity.Error)
            .WithLocation(0)
            .WithArguments("LogIt");

        await RunAsync(code, expected);
    }

    [Fact]
    public async Task MoreThanSixParameters_ReportsTooManyParameters()
    {
        var code = @"
using NexusLabs.Needlr.Logging;
using Microsoft.Extensions.Logging;

public partial class Target
{
    private ILogger _logger;

    [NeedlrLoggerMessage]
    partial void {|#0:LogIt|}(int a, int b, int c, int d, int e, int f, int g);
}
" + Preamble;

        var expected = new DiagnosticResult(DiagnosticIds.TooManyParameters, DiagnosticSeverity.Info)
            .WithLocation(0)
            .WithArguments("LogIt", 7);

        await RunAsync(code, expected);
    }

    private static async Task RunAsync(string code, params DiagnosticResult[] expected)
    {
        var test = new CSharpAnalyzerTest<NeedlrLoggerMessageAnalyzer, DefaultVerifier>
        {
            TestCode = code,
        };

        test.ExpectedDiagnostics.AddRange(expected);

        await test.RunAsync(TestContext.Current.CancellationToken);
    }
}
