namespace MakroPad.App.ViewModels;

public sealed record DisplayTriggerPreset(
    string Name,
    string EventType,
    string Input,
    string ActionStatus,
    string ConnectionStatus,
    string Condition,
    int MinPercent,
    int MaxPercent,
    int ChangePercent,
    int DurationMs,
    string TemplateMode,
    string Template);
