using Xunit;

using static NexusLabs.Needlr.Generators.Tests.Diagnostics.DiagnosticTestHelpers;

namespace NexusLabs.Needlr.Generators.Tests.Diagnostics;

/// <summary>
/// Tests for OptionsSummary.md diagnostic output generation.
/// </summary>
public sealed class OptionsSummaryDiagnosticTests
{
    [Fact]
    public void OptionsSummary_NoOptionsClasses_GeneratesEmptyMessage()
    {
        var source = @"
using NexusLabs.Needlr.Generators;

[assembly: GenerateTypeRegistry]

namespace TestApp
{
    public interface IService { }
    public class MyService : IService { }
}";

        var content = GetDiagnosticContent(source, "OptionsSummary");

        Assert.Contains("Needlr Options Summary", content);
        Assert.Contains("No options classes discovered", content);
    }

    [Fact]
    public void OptionsSummary_SingleOptionsClass_IncludesSectionName()
    {
        var source = @"
using NexusLabs.Needlr;
using NexusLabs.Needlr.Generators;

[assembly: GenerateTypeRegistry]

namespace TestApp
{
    [Options(""Database"")]
    public class DatabaseOptions
    {
        public string ConnectionString { get; set; } = """";
    }
}";

        var content = GetDiagnosticContent(source, "OptionsSummary");

        Assert.Contains("Needlr Options Summary", content);
        Assert.Contains("DatabaseOptions", content);
        Assert.Contains("`Database`", content);
        Assert.Contains("Total Options Classes | 1", content);
    }

    [Fact]
    public void OptionsSummary_NamedOptions_ShowsNameColumn()
    {
        var source = @"
using NexusLabs.Needlr;
using NexusLabs.Needlr.Generators;

[assembly: GenerateTypeRegistry]

namespace TestApp
{
    [Options(""Connections"", Name = ""Primary"")]
    public class ConnectionOptions
    {
        public string Host { get; set; } = """";
    }
}";

        var content = GetDiagnosticContent(source, "OptionsSummary");

        Assert.Contains("ConnectionOptions", content);
        Assert.Contains("Primary", content);
        Assert.Contains("Named Options | 1", content);
    }

    [Fact]
    public void OptionsSummary_ValidateOnStart_ShowsCheckmark()
    {
        var source = @"
using NexusLabs.Needlr;
using NexusLabs.Needlr.Generators;

[assembly: GenerateTypeRegistry]

namespace TestApp
{
    [Options(""App"", ValidateOnStart = true)]
    public class AppOptions
    {
        public string Name { get; set; } = """";
    }
}";

        var content = GetDiagnosticContent(source, "OptionsSummary");

        Assert.Contains("AppOptions", content);
        Assert.Contains("✅", content);
        Assert.Contains("With Validation | 1", content);
    }

    [Fact]
    public void OptionsSummary_ValidateOnStartFalse_ShowsX()
    {
        var source = @"
using NexusLabs.Needlr;
using NexusLabs.Needlr.Generators;

[assembly: GenerateTypeRegistry]

namespace TestApp
{
    [Options(""App"", ValidateOnStart = false)]
    public class AppOptions
    {
        public string Name { get; set; } = """";
    }
}";

        var content = GetDiagnosticContent(source, "OptionsSummary");

        Assert.Contains("AppOptions", content);
        Assert.Contains("❌", content);
    }

    [Fact]
    public void OptionsSummary_MultipleOptions_ShowsAll()
    {
        var source = @"
using NexusLabs.Needlr;
using NexusLabs.Needlr.Generators;

[assembly: GenerateTypeRegistry]

namespace TestApp
{
    [Options(""Database"")]
    public class DatabaseOptions
    {
        public string ConnectionString { get; set; } = """";
    }

    [Options(""Cache"")]
    public class CacheOptions
    {
        public int TimeoutSeconds { get; set; }
    }

    [Options(""Logging"")]
    public class LoggingOptions
    {
        public string Level { get; set; } = ""Info"";
    }
}";

        var content = GetDiagnosticContent(source, "OptionsSummary");

        Assert.Contains("DatabaseOptions", content);
        Assert.Contains("CacheOptions", content);
        Assert.Contains("LoggingOptions", content);
        Assert.Contains("Total Options Classes | 3", content);
    }

    [Fact]
    public void OptionsSummary_WithSelfValidation_ShowsValidationMethod()
    {
        var source = @"
using System.Collections.Generic;
using NexusLabs.Needlr;
using NexusLabs.Needlr.Generators;

[assembly: GenerateTypeRegistry]

namespace TestApp
{
    [Options(""Database"", ValidateOnStart = true)]
    public class DatabaseOptions
    {
        public string ConnectionString { get; set; } = """";

        public IEnumerable<string> Validate()
        {
            if (string.IsNullOrEmpty(ConnectionString))
                yield return ""ConnectionString is required"";
        }
    }
}";

        var content = GetDiagnosticContent(source, "OptionsSummary");

        Assert.Contains("DatabaseOptions", content);
        Assert.Contains("Validate()", content);
    }

    [Fact]
    public void OptionsSummary_TableStructure_IsValid()
    {
        var source = @"
using NexusLabs.Needlr;
using NexusLabs.Needlr.Generators;

[assembly: GenerateTypeRegistry]

namespace TestApp
{
    [Options(""Database"")]
    public class DatabaseOptions
    {
        public string ConnectionString { get; set; } = """";
    }
}";

        var content = GetDiagnosticContent(source, "OptionsSummary");

        Assert.Contains("| Class | Section | Name | ValidateOnStart | Validator |", content);
        Assert.Contains("|:------|:--------|:-----|:---------------:|:----------|", content);
    }

    [Fact]
    public void OptionsSummary_ConfigurationSections_GroupedCorrectly()
    {
        var source = @"
using NexusLabs.Needlr;
using NexusLabs.Needlr.Generators;

[assembly: GenerateTypeRegistry]

namespace TestApp
{
    [Options(""Database"")]
    public class DatabaseOptions { }

    [Options(""Database:Primary"")]
    public class PrimaryDbOptions { }
}";

        var content = GetDiagnosticContent(source, "OptionsSummary");

        Assert.Contains("## Configuration Sections", content);
        Assert.Contains("### `Database`", content);
        Assert.Contains("### `Database:Primary`", content);
    }

    [Fact]
    public void OptionsSummary_UsageSection_Included()
    {
        var source = @"
using NexusLabs.Needlr;
using NexusLabs.Needlr.Generators;

[assembly: GenerateTypeRegistry]

namespace TestApp
{
    [Options(""App"")]
    public class AppOptions { }
}";

        var content = GetDiagnosticContent(source, "OptionsSummary");

        Assert.Contains("## Usage", content);
        Assert.Contains("IOptions<", content);
        Assert.Contains("IOptionsSnapshot<", content);
    }

    [Fact]
    public void OptionsSummary_ValidatorWithoutValidateOnStart_ShowsWarning()
    {
        var source = @"
using System.Collections.Generic;
using NexusLabs.Needlr;
using NexusLabs.Needlr.Generators;

[assembly: GenerateTypeRegistry]

namespace TestApp
{
    [Options(""Database"", ValidateOnStart = false)]
    public class DatabaseOptions
    {
        public string ConnectionString { get; set; } = """";

        public IEnumerable<string> Validate()
        {
            if (string.IsNullOrEmpty(ConnectionString))
                yield return ""ConnectionString is required"";
        }
    }
}";

        var content = GetDiagnosticContent(source, "OptionsSummary");

        Assert.Contains("⚠️ Potential Issues", content);
        Assert.Contains("validator but won't run", content);
    }
}
