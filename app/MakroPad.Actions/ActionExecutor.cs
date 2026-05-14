namespace MakroPad.Actions;

public sealed class ActionExecutor
{
    private readonly ISystemMediaController mediaController;

    public ActionExecutor()
        : this(SystemMediaControllerFactory.Create())
    {
    }

    public ActionExecutor(ISystemMediaController mediaController)
    {
        this.mediaController = mediaController;
    }

    public Task<ActionExecutionResult> ExecuteAsync(
        ActionExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return context.Binding.Action switch
        {
            "media.playPause" => mediaController.PlayPauseAsync(cancellationToken),
            "system.volume" => Task.FromResult(ActionExecutionResult.Ok($"Volume action matched: {context.Value ?? "n/a"}")),
            "log" => Task.FromResult(ActionExecutionResult.Ok(context.Binding.Parameters.GetValueOrDefault("message", "Log action matched"))),
            "" => Task.FromResult(ActionExecutionResult.Failed("Action is empty")),
            _ => Task.FromResult(ActionExecutionResult.Failed($"Unsupported action: {context.Binding.Action}"))
        };
    }
}
