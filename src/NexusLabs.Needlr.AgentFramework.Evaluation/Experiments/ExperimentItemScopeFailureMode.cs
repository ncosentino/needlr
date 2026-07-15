namespace NexusLabs.Needlr.AgentFramework.Evaluation.Experiments;

/// <summary>
/// Defines whether an item-scope setup failure may prevent a task attempt from starting.
/// </summary>
public enum ExperimentItemScopeFailureMode
{
    /// <summary>
    /// Records scope failures as publication failures while allowing item quality processing to
    /// continue.
    /// </summary>
    BestEffort,

    /// <summary>
    /// Prevents the next task attempt from starting when scope entry or activation fails.
    /// </summary>
    ExecutionPrerequisite,
}
