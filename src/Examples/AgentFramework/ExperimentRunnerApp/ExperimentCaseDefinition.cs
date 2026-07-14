namespace ExperimentRunnerApp;

/// <summary>
/// Defines deterministic behavior for one example experiment case.
/// </summary>
internal sealed record ExperimentCaseDefinition(
    string Mode,
    int Value,
    int DelayMilliseconds,
    double SuccessProbability);
