using System;
using System.Linq;

namespace MakroPad.App.ViewModels;

public sealed record DisplayTriggerViewModel(
    int Index,
    bool Enabled,
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
    string Template)
{
    public string SequenceDisplay => (Index + 1).ToString();

    public string EnabledDisplay => Enabled ? "Yes" : "No";

    public string ChangeDisplay => EventType == "input"
        && Input.StartsWith("slider:", StringComparison.OrdinalIgnoreCase)
            ? $"{ChangePercent}%"
            : "-";

    public string TemplateSummary
    {
        get
        {
            var firstLine = Template
                .Replace("\r\n", "\n")
                .Replace('\r', '\n')
                .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .FirstOrDefault();

            return string.IsNullOrWhiteSpace(firstLine) ? "(empty)" : firstLine;
        }
    }

    public string ConditionDisplay => Condition == "inRange"
        ? $"{Condition} {MinPercent}-{MaxPercent}%"
        : Condition;

    public string MatchDisplay => EventType == "actionResult"
        ? $"{Input} / {ActionStatus}"
        : EventType == "mediaChanged"
            ? "*"
            : EventType == "connectionChanged"
                ? ConnectionStatus
                : EventType == "profileChanged"
                    ? "*"
                    : $"{Input} / {ConditionDisplay}";
}
