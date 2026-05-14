using MakroPad.Core.Configuration;

namespace MakroPad.Actions;

public sealed class ActionPipeline
{
    private static readonly TimeSpan SliderThrottleInterval = TimeSpan.FromMilliseconds(100);

    private readonly ActionResolver actionResolver;
    private readonly ActionExecutor actionExecutor;
    private readonly Dictionary<string, DateTimeOffset> lastSliderExecutionByInput = [];

    public ActionPipeline(ActionResolver actionResolver, ActionExecutor actionExecutor)
    {
        this.actionResolver = actionResolver;
        this.actionExecutor = actionExecutor;
    }

    public async Task<ActionPipelineResult> HandleInputAsync(
        MakroPadConfig config,
        string input,
        string? value = null,
        CancellationToken cancellationToken = default)
    {
        if (ShouldThrottle(input))
        {
            return ActionPipelineResult.Throttled(input);
        }

        var binding = actionResolver.Resolve(config, input);
        if (binding is null)
        {
            return ActionPipelineResult.Unbound(input, config.ActiveProfile);
        }

        var executionResult = await actionExecutor.ExecuteAsync(
            new ActionExecutionContext(input, binding, value),
            cancellationToken);

        return ActionPipelineResult.Executed(input, binding, executionResult);
    }

    private bool ShouldThrottle(string input)
    {
        if (!input.StartsWith("slider:", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var now = DateTimeOffset.UtcNow;
        if (lastSliderExecutionByInput.TryGetValue(input, out var lastExecution) &&
            now - lastExecution < SliderThrottleInterval)
        {
            return true;
        }

        lastSliderExecutionByInput[input] = now;
        return false;
    }
}
