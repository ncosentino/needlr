namespace WorkerServiceExample;

/// <summary>
/// Example service interface for dependency injection.
/// </summary>
public interface IExampleService
{
    string GetMessage();
}

/// <summary>
/// Example service that Needlr auto-discovers and registers as a singleton.
/// </summary>
public sealed class ExampleService : IExampleService
{
    private int _counter;

    public string GetMessage()
    {
        return $"Worker service running (iteration {++_counter})";
    }
}
