namespace NexusLabs.Needlr.Generators.Models;

/// <summary>
/// Information about an interface implemented by a service, including its source location.
/// </summary>
internal readonly struct InterfaceInfo
{
    public InterfaceInfo(string fullName, string? sourceFilePath = null, int sourceLine = 0)
    {
        FullName = fullName;
        SourceFilePath = sourceFilePath;
        SourceLine = sourceLine;
    }

    public string FullName { get; }
    public string? SourceFilePath { get; }
    public int SourceLine { get; }

    public bool HasLocation => SourceFilePath != null && SourceLine > 0;
}
