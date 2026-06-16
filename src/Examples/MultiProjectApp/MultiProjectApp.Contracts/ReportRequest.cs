namespace MultiProjectApp.Contracts;

/// <summary>
/// A domain contract carried by the contracts library. Records are not auto-registered by Needlr,
/// so this assembly has nothing to register yet still participates via a minimal TypeRegistry.
/// </summary>
/// <param name="Title">The report title.</param>
/// <param name="Body">The report body.</param>
/// <param name="Severity">The report severity.</param>
public sealed record ReportRequest(string Title, string Body, ReportSeverity Severity);
