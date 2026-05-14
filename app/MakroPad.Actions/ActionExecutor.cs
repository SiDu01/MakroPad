namespace MakroPad.Actions;

public sealed class ActionExecutor
{
    public Task<ActionExecutionResult> ExecuteAsync(
        ActionExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var result = context.Binding.Action switch
        {
            "media.playPause" => ActionExecutionResult.Ok("Play/Pause action matched"),
            "system.volume" => ActionExecutionResult.Ok($"Volume action matched: {context.Value ?? "n/a"}"),
            "log" => ActionExecutionResult.Ok(context.Binding.Parameters.GetValueOrDefault("message", "Log action matched")),
            "" => ActionExecutionResult.Failed("Action is empty"),
            _ => ActionExecutionResult.Failed($"Unsupported action: {context.Binding.Action}")
        };

        return Task.FromResult(result);
    }
}
