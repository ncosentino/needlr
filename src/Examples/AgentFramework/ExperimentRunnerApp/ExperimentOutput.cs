namespace ExperimentRunnerApp;

/// <summary>
/// Represents one successful example task output.
/// </summary>
internal sealed record ExperimentOutput(
    int Value,
    string Category,
    bool Passed);
