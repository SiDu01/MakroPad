using System.Globalization;
using System.Runtime.CompilerServices;

namespace MakroPad.Core.Actions;

/// <summary>Runs bindings through their handlers; <c>macro</c> and <c>toggle</c> bindings are run here step by step.</summary>
public sealed class ActionRunner
{
    private readonly Dictionary<string, IActionHandler> handlers;
    private readonly TimeProvider timeProvider;

    // Next toggle step per binding; a reloaded config has new bindings, so toggles start at their first step again.
    private readonly ConditionalWeakTable<Binding, StrongBox<int>> toggleNext = new();

    public ActionRunner(IEnumerable<IActionHandler> handlers, TimeProvider? timeProvider = null)
    {
        this.handlers = handlers.ToDictionary(handler => handler.ActionId, StringComparer.OrdinalIgnoreCase);
        this.timeProvider = timeProvider ?? TimeProvider.System;
    }

    public bool IsKnown(string actionId)
        => handlers.ContainsKey(actionId) || ActionCatalog.HasSteps(actionId);

    public async Task<ActionResult> RunAsync(ActionContext context, CancellationToken cancellationToken = default)
    {
        if (string.Equals(context.Binding.Action, "macro", StringComparison.OrdinalIgnoreCase))
        {
            return await RunMacroAsync(context, cancellationToken);
        }

        if (string.Equals(context.Binding.Action, "toggle", StringComparison.OrdinalIgnoreCase))
        {
            return await RunToggleAsync(context, cancellationToken);
        }

        if (!handlers.TryGetValue(context.Binding.Action, out var handler))
        {
            return ActionResult.Failed($"Unbekannte Aktion: {context.Binding.Action}");
        }

        try
        {
            return await handler.ExecuteAsync(context, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return ActionResult.Failed($"{context.Binding.Action}: {ex.Message}");
        }
    }

    /// <summary>Runs the steps in order and stops at the first failing one.</summary>
    private async Task<ActionResult> RunMacroAsync(ActionContext context, CancellationToken cancellationToken)
    {
        var steps = context.Binding.Steps ?? [];
        if (steps.Count == 0)
        {
            return ActionResult.Failed("Das Makro hat keine Schritte");
        }

        for (var index = 0; index < steps.Count; index++)
        {
            var result = await RunStepAsync(context, steps[index], cancellationToken);
            if (!result.Success)
            {
                return ActionResult.Failed($"Schritt {index + 1}: {result.Message}");
            }
        }

        var actions = steps.Count(s => !string.Equals(s.Action, "delay", StringComparison.OrdinalIgnoreCase));
        return ActionResult.Ok($"Makro, {actions} {(actions == 1 ? "Aktion" : "Aktionen")}");
    }

    /// <summary>Runs the next step; a failed step is tried again on the next press.</summary>
    private async Task<ActionResult> RunToggleAsync(ActionContext context, CancellationToken cancellationToken)
    {
        var steps = context.Binding.Steps ?? [];
        if (steps.Count == 0)
        {
            return ActionResult.Failed("Abwechselnd hat keine Schritte");
        }

        var next = toggleNext.GetOrCreateValue(context.Binding);
        int index;
        lock (next)
        {
            index = next.Value % steps.Count;
        }

        var result = await RunStepAsync(context, steps[index], cancellationToken);
        if (!result.Success)
        {
            return ActionResult.Failed($"Schritt {index + 1}: {result.Message}");
        }

        lock (next)
        {
            next.Value = index + 1;
        }

        return result;
    }

    private async Task<ActionResult> RunStepAsync(ActionContext context, MacroStep step, CancellationToken cancellationToken)
    {
        if (string.Equals(step.Action, "delay", StringComparison.OrdinalIgnoreCase))
        {
            var ms = int.TryParse(step.GetParameter("ms"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
                ? Math.Clamp(value, 0, ActionCatalog.MaxDelayMs)
                : 0;
            await Task.Delay(TimeSpan.FromMilliseconds(ms), timeProvider, cancellationToken);
            return ActionResult.Ok($"{ms} ms gewartet");
        }

        if (ActionCatalog.HasSteps(step.Action))
        {
            return ActionResult.Failed($"{step.Action} geht nicht als Schritt");
        }

        return await RunAsync(context with { Binding = new Binding(context.Binding.Input, step.Action, step.Parameters) }, cancellationToken);
    }
}
