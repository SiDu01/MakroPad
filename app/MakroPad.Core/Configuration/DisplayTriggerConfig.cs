namespace MakroPad.Core.Configuration;

public sealed record DisplayTriggerConfig
{
    public bool Enabled { get; init; }

    public string EventType { get; init; } = "input";

    public string Input { get; init; } = "slider:1";

    public string ActionStatus { get; init; } = "any";

    public string Condition { get; init; } = "any";

    public int MinPercent { get; init; } = 0;

    public int MaxPercent { get; init; } = 100;

    public int ChangePercent { get; init; } = 5;

    public int DurationMs { get; init; } = 3000;

    public string TemplateMode { get; init; } = "normal";

    public string Template { get; init; } = "Slider {slider.id}\n{slider.percent}%";
}
