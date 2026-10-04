namespace MakroPad.Core.Actions;

public sealed class ActionRunner
{
    private readonly Dictionary<string, IActionHandler> handlers;

    public ActionRunner(IEnumerable<IActionHandler> handlers)
    {
        this.handlers = handlers.ToDictionary(handler => handler.ActionId, StringComparer.OrdinalIgnoreCase);
    }

    public bool IsKnown(string actionId) => handlers.ContainsKey(actionId);

    public async Task<ActionResult> RunAsync(ActionContext context, CancellationToken cancellationToken = default)
    {
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
}
