namespace MakroPad.Core.Configuration;

/// <summary>
/// One configurable idle (default) screen. The app picks the active idle screen based on its
/// <see cref="Activation"/> and pushes it to the device. Content is authored exactly like a
/// display rule (Template + TemplateMode).
/// </summary>
public sealed record IdleScreenConfig
{
    public string Name { get; init; } = "Idle";

    public bool Enabled { get; init; } = true;

    public string Template { get; init; } = "{profile}\n{status}";

    /// <summary>normal | markdown | html</summary>
    public string TemplateMode { get; init; } = "markdown";

    /// <summary>default | process</summary>
    public string Activation { get; init; } = "default";

    /// <summary>Process name to match when <see cref="Activation"/> is "process" (without .exe).</summary>
    public string ProcessName { get; init; } = "";
}
