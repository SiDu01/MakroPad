using MakroPad.Core.Configuration;

namespace MakroPad.Actions;

public sealed record ActionPipelineResult(
    ActionPipelineResultKind Kind,
    string Input,
    string Message,
    ActionBinding? Binding = null,
    ActionExecutionResult? ExecutionResult = null)
{
    public bool ShouldSendFeedback => Kind is ActionPipelineResultKind.Executed;

    public static ActionPipelineResult Executed(
        string input,
        ActionBinding binding,
        ActionExecutionResult executionResult)
    {
        return new ActionPipelineResult(
            ActionPipelineResultKind.Executed,
            input,
            executionResult.Message,
            binding,
            executionResult);
    }

    public static ActionPipelineResult Throttled(string input)
    {
        return new ActionPipelineResult(ActionPipelineResultKind.Throttled, input, "Action throttled");
    }

    public static ActionPipelineResult Unbound(string input, string profile)
    {
        return new ActionPipelineResult(
            ActionPipelineResultKind.Unbound,
            input,
            $"No action binding for {input} in profile {profile}.");
    }
}
