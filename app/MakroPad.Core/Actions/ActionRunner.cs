using System.Globalization;

namespace MakroPad.Core.Actions;

/// <summary>Runs bindings through their handlers; <c>macro</c> bindings are run here step by step.</summary>
public sealed class ActionRunner
{
    private readonly Dictionary<string, IActionHandler> handlers;
    private readonly TimeProvider timeProvider;

    public ActionRunner(IEnumerable<IActionHandler> handlers, TimeProvider? timeProvider = null)
    {
        this.handlers = handlers.ToDictionary(handler => handler.ActionId, StringComparer.OrdinalIgnoreCase);
        this.timeProvider = timeProvider ?? TimeProvider.System;
    }

    public bool IsKnown(string actionId)
        => handlers.ContainsKey(actionId) || string.Equals(actionId, "macro", StringComparison.OrdinalIgnoreCase);

    public async Task<ActionResult> RunAsync(ActionContext context, CancellationToken cancellationToken = default)
    {
        if (string.Equals(context.Binding.Action, "macro", StringComparison.OrdinalIgnoreCase))
        {
            return await RunMacroAsync(context, cancellationToken);
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
            var step = steps[index];
            ActionResult result;
            if (string.Equals(step.Action, "delay", StringComparison.OrdinalIgnoreCase))
            {
                var ms = int.TryParse(step.GetParameter("ms"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
                    ? Math.Clamp(value, 0, ActionCatalog.MaxDelayMs)
                    : 0;
                await Task.Delay(TimeSpan.FromMilliseconds(ms), timeProvider, cancellationToken);
                continue;
            }

            if (string.Equals(step.Action, "macro", StringComparison.OrdinalIgnoreCase))
            {
                result = ActionResult.Failed("Makros können keine Makros enthalten");
            }
            else
            {
                var stepBinding = new Binding(context.Binding.Input, step.Action, step.Parameters);
                result = await RunAsync(context with { Binding = stepBinding }, cancellationToken);
            }

            if (!result.Success)
            {
                return ActionResult.Failed($"Schritt {index + 1}: {result.Message}");
            }
        }

        var actions = steps.Count(s => !string.Equals(s.Action, "delay", StringComparison.OrdinalIgnoreCase));
        return ActionResult.Ok($"Makro, {actions} {(actions == 1 ? "Aktion" : "Aktionen")}");
    }
}
