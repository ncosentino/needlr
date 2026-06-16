namespace MultiProjectApp.Contracts;

/// <summary>
/// Severity levels for a <see cref="ReportRequest"/>. Enums are not registerable, reinforcing that
/// this contracts library is a pure-domain participant with nothing for Needlr to register.
/// </summary>
public enum ReportSeverity
{
    /// <summary>Informational report.</summary>
    Info,

    /// <summary>Warning report.</summary>
    Warning,

    /// <summary>Critical report.</summary>
    Critical,
}
