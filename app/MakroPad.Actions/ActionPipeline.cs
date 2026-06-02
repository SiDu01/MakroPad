using MakroPad.Core.Configuration;

namespace MakroPad.Actions;

public sealed class ActionPipeline
{
    private static readonly TimeSpan SliderThrottleInterval = TimeSpan.FromMilliseconds(100);
    private const int SliderVolumePercentDeadband = 2;

    private readonly ActionResolver actionResolver;
    private readonly ActionExecutor actionExecutor;
    private readonly Dictionary<string, DateTimeOffset> lastSliderExecutionByInput = [];
    private readonly Dictionary<string, int> lastSliderVolumePercentByInput = [];

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

        if (ShouldThrottleSliderVolumeChange(input, binding, value))
        {
            return ActionPipelineResult.Throttled(input);
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

    private bool ShouldThrottleSliderVolumeChange(string input, ActionBinding binding, string? value)
    {
        if (!input.StartsWith("slider:", StringComparison.OrdinalIgnoreCase)
            || !IsSliderVolumeAction(binding.Action)
            || !TryMapSliderValueToPercent(value, out var percent))
        {
            return false;
        }

        var key = $"{input}:{binding.Action}";
        if (lastSliderVolumePercentByInput.TryGetValue(key, out var lastPercent)
            && percent is not 0 and not 100
            && Math.Abs(percent - lastPercent) < SliderVolumePercentDeadband)
        {
            return true;
        }

        lastSliderVolumePercentByInput[key] = percent;
        return false;
    }

    private static bool IsSliderVolumeAction(string action)
    {
        return action.Equals("system.volume", StringComparison.OrdinalIgnoreCase)
            || action.Equals("app.volume", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryMapSliderValueToPercent(string? sliderValue, out int percent)
    {
        percent = 0;
        if (!int.TryParse(sliderValue, out var rawValue))
        {
            return false;
        }

        var clampedRawValue = Math.Clamp(rawValue, 0, 4095);
        percent = (int)Math.Round(clampedRawValue / 4095.0 * 100);
        return true;
    }
}
