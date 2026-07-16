namespace NexusLabs.Needlr.AgentFramework.Langfuse;

/// <summary>
/// Creates built-in hosted and local Langfuse experiment result sinks.
/// </summary>
internal interface ILangfuseExperimentResultSinkFactory
{
    LangfuseExperimentResultSink<TCase, TOutput>
        CreateExperimentResultSink<TCase, TOutput>(
            ILangfuseExperimentRun run,
            LangfuseExperimentResultSinkOptions<TCase, TOutput>? options);

    LangfuseExperimentResultSink<TCase, TOutput>
        CreateLocalExperimentResultSink<TCase, TOutput>(
            LangfuseExperimentResultSinkOptions<TCase, TOutput>? options);
}
