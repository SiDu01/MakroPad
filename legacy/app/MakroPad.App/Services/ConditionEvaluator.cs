using System;
using System.Collections.Generic;
using System.Linq;
using MakroPad.Core.Configuration;

namespace MakroPad.App.Services;

/// <summary>
/// Pure evaluation of idle-screen conditions against a <see cref="SystemStateSnapshot"/>. Kept free
/// of any OS calls so it can be unit-tested with synthetic snapshots.
/// </summary>
public static class ConditionEvaluator
{
    /// <summary>
    /// True when the screen's conditions are satisfied. An empty condition list means the screen is
    /// always eligible (the classic "default" screen).
    /// </summary>
    public static bool IsEligible(IdleScreenConfig screen, SystemStateSnapshot state)
    {
        if (screen.Conditions is null || screen.Conditions.Count == 0)
        {
            return true;
        }

        var isOr = string.Equals(screen.ConditionLogic, "or", StringComparison.OrdinalIgnoreCase);
        return isOr
            ? screen.Conditions.Any(c => IsSatisfied(c, state))
            : screen.Conditions.All(c => IsSatisfied(c, state));
    }

    public static bool IsSatisfied(IdleScreenCondition condition, SystemStateSnapshot state)
    {
        var result = Evaluate(condition, state);
        return condition.Negate ? !result : result;
    }

    private static bool Evaluate(IdleScreenCondition condition, SystemStateSnapshot state) =>
        (condition.Type ?? "").Trim().ToLowerInvariant() switch
        {
            "processrunning" => Contains(state.RunningProcesses, condition.Target),
            "mediaactive" => Contains(state.MediaActiveProcesses, condition.Target),
            "processforeground" => MatchesForeground(state.ForegroundProcess, condition.Target),
            "fullscreen" => state.ForegroundIsFullscreen
                            && (IsAny(condition.Target) || MatchesForeground(state.ForegroundProcess, condition.Target)),
            "systemmetric" => EvaluateMetric(condition, state),
            _ => false
        };

    private static bool Contains(IReadOnlySet<string> processes, string target)
    {
        var name = Normalize(target);
        return !string.IsNullOrEmpty(name) && processes.Contains(name);
    }

    private static bool MatchesForeground(string? foreground, string target)
    {
        var name = Normalize(target);
        return !string.IsNullOrEmpty(name)
               && string.Equals(Normalize(foreground), name, StringComparison.OrdinalIgnoreCase);
    }

    private static bool EvaluateMetric(IdleScreenCondition condition, SystemStateSnapshot state)
    {
        var value = (condition.Metric ?? "").Trim().ToLowerInvariant() switch
        {
            "cpu" => state.CpuPercent,
            "memory" => state.MemoryPercent,
            "battery" => state.BatteryPercent,
            "network" => state.NetworkKbps,
            _ => double.NaN
        };

        if (double.IsNaN(value))
        {
            return false;
        }

        return (condition.Comparison ?? "").Trim().ToLowerInvariant() switch
        {
            "gt" => value > condition.Threshold,
            "gte" => value >= condition.Threshold,
            "lt" => value < condition.Threshold,
            "lte" => value <= condition.Threshold,
            _ => false
        };
    }

    private static bool IsAny(string target) => string.IsNullOrWhiteSpace(target);

    private static string Normalize(string? processName)
    {
        var name = (processName ?? "").Trim();
        if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            name = name[..^4];
        }

        return name;
    }
}
