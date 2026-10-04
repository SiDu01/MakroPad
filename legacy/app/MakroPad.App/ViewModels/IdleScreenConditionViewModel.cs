using System;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using MakroPad.Core.Configuration;

namespace MakroPad.App.ViewModels;

/// <summary>One activation condition of an idle screen, edited live in the list.</summary>
public partial class IdleScreenConditionViewModel : ObservableObject
{
    private readonly Action? onChanged;

    public IdleScreenConditionViewModel(IdleScreenCondition condition, Action? onChanged = null)
    {
        this.onChanged = onChanged;
        type = string.IsNullOrWhiteSpace(condition.Type) ? "processRunning" : condition.Type;
        target = condition.Target;
        metric = string.IsNullOrWhiteSpace(condition.Metric) ? "cpu" : condition.Metric;
        comparison = string.IsNullOrWhiteSpace(condition.Comparison) ? "gt" : condition.Comparison;
        threshold = condition.Threshold;
        negate = condition.Negate;
    }

    [ObservableProperty]
    private string type;

    [ObservableProperty]
    private string target;

    [ObservableProperty]
    private string metric;

    [ObservableProperty]
    private string comparison;

    [ObservableProperty]
    private double threshold;

    [ObservableProperty]
    private bool negate;

    /// <summary>True for the metric condition (shows metric/comparison/threshold instead of a target).</summary>
    public bool IsMetric => string.Equals(Type, "systemMetric", StringComparison.OrdinalIgnoreCase);

    /// <summary>True when the condition is driven by a process/app target.</summary>
    public bool UsesTarget => !IsMetric;

    public string Summary
    {
        get
        {
            var body = Type.ToLowerInvariant() switch
            {
                "processrunning" => $"läuft: {TargetOrAny}",
                "processforeground" => $"Fokus: {TargetOrAny}",
                "fullscreen" => string.IsNullOrWhiteSpace(Target) ? "Vollbild" : $"Vollbild: {Target}",
                "mediaactive" => $"Medien: {TargetOrAny}",
                "systemmetric" => $"{Metric} {Comparison} {Threshold.ToString(CultureInfo.InvariantCulture)}",
                _ => Type
            };

            return Negate ? $"nicht {body}" : body;
        }
    }

    private string TargetOrAny => string.IsNullOrWhiteSpace(Target) ? "?" : Target;

    public IdleScreenCondition ToConfig() => new()
    {
        Type = Type,
        Target = (Target ?? "").Trim(),
        Metric = Metric,
        Comparison = Comparison,
        Threshold = Threshold,
        Negate = Negate
    };

    partial void OnTypeChanged(string value)
    {
        OnPropertyChanged(nameof(IsMetric));
        OnPropertyChanged(nameof(UsesTarget));
        Notify();
    }

    partial void OnTargetChanged(string value) => Notify();

    partial void OnMetricChanged(string value) => Notify();

    partial void OnComparisonChanged(string value) => Notify();

    partial void OnThresholdChanged(double value) => Notify();

    partial void OnNegateChanged(bool value) => Notify();

    private void Notify()
    {
        OnPropertyChanged(nameof(Summary));
        onChanged?.Invoke();
    }
}
