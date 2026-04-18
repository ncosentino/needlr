namespace IterativeTripPlannerApp.Core;

/// <summary>
/// Configuration for a trip planner run.
/// </summary>
public sealed record TripPlannerConfig(
    string Origin,
    string Destination,
    int MaxStops,
    int MinStops,
    string Budget);
