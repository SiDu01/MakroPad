namespace MakroPad.Core.Configuration;

/// <summary>
/// One activation condition for an idle screen. A screen is "eligible" when its conditions are
/// satisfied (combined via <see cref="IdleScreenConfig.ConditionLogic"/>). Heterogeneous condition
/// types share this record; only the fields relevant to <see cref="Type"/> are used.
/// </summary>
public sealed record IdleScreenCondition
{
    /// <summary>
    /// processRunning | processForeground | fullscreen | mediaActive | systemMetric
    /// </summary>
    public string Type { get; init; } = "processRunning";

    /// <summary>Process name (without .exe) the condition applies to. Empty means "any app".</summary>
    public string Target { get; init; } = "";

    /// <summary>cpu | memory | battery | network (only for <see cref="Type"/> == "systemMetric").</summary>
    public string Metric { get; init; } = "";

    /// <summary>gt | gte | lt | lte (only for "systemMetric").</summary>
    public string Comparison { get; init; } = "gt";

    /// <summary>Threshold compared against the metric value (only for "systemMetric").</summary>
    public double Threshold { get; init; }

    /// <summary>Inverts the result of the condition ("NOT").</summary>
    public bool Negate { get; init; }
}
