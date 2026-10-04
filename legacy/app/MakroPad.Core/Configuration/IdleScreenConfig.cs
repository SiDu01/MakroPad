namespace MakroPad.Core.Configuration;

/// <summary>
/// One configurable idle (default) screen. The app picks the active idle screen by evaluating each
/// screen's <see cref="Conditions"/> and pushes the winner to the device. Content is authored
/// exactly like a display rule (Template + TemplateMode).
/// </summary>
public sealed record IdleScreenConfig
{
    public string Name { get; init; } = "Idle";

    public bool Enabled { get; init; } = true;

    public string Template { get; init; } = "{profile}\n{status}";

    /// <summary>normal | markdown | html</summary>
    public string TemplateMode { get; init; } = "markdown";

    /// <summary>
    /// Conditions that make this screen eligible. An empty list means the screen is always eligible
    /// (the classic "default" screen). Combined via <see cref="ConditionLogic"/>.
    /// </summary>
    public List<IdleScreenCondition> Conditions { get; init; } = [];

    /// <summary>and | or — how to combine multiple <see cref="Conditions"/>.</summary>
    public string ConditionLogic { get; init; } = "and";

    /// <summary>Higher priority wins; only the highest-priority eligible tier is shown.</summary>
    public int Priority { get; init; }

    /// <summary>When eligible, this screen is shown exclusively (suppresses rotation of others).</summary>
    public bool Exclusive { get; init; }

    /// <summary>Conditions must hold continuously for this long before the screen becomes eligible.</summary>
    public int ActivationDelaySeconds { get; init; }

    /// <summary>Once shown, the screen stays for at least this long before switching away (anti-flicker).</summary>
    public int MinDwellSeconds { get; init; }

    // ----- Legacy fields (kept for deserializing pre-conditions configs; migrated into Conditions) -----

    /// <summary>default | process — legacy activation; superseded by <see cref="Conditions"/>.</summary>
    public string Activation { get; init; } = "default";

    /// <summary>Legacy process name to match when <see cref="Activation"/> was "process".</summary>
    public string ProcessName { get; init; } = "";
}
