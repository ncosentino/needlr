using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

internal static class KernelExtensions
{
    public static async Task AskAsync(
        this Kernel kernel, 
        string question)
    {
        var behavior = FunctionChoiceBehavior.Auto(
            autoInvoke: true,
            options: new FunctionChoiceBehaviorOptions
            {
                AllowParallelCalls = false,
                AllowConcurrentInvocation = false
            });
        var settings = new PromptExecutionSettings
        {
            FunctionChoiceBehavior = behavior
        };

        Console.WriteLine("QUESTION: " + question);
        var result = await kernel
            .GetRequiredService<IChatCompletionService>()
            .GetChatMessageContentAsync(
                question,
                executionSettings: settings,
                kernel: kernel);
        Console.WriteLine("ANSWER: " + result.Content);
    }
}