namespace NexusLabs.Needlr.AgentFramework.Evaluation.Reporting.Tests;

internal sealed class TemporaryTestDirectory : IDisposable
{
    public TemporaryTestDirectory()
    {
        Path = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "needlr-meai-reporting-tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path);
    }

    public string Path { get; }

    public void Dispose()
    {
        if (Directory.Exists(Path))
        {
            Directory.Delete(Path, recursive: true);
        }
    }
}
