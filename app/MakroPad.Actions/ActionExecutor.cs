namespace MakroPad.Actions;

public sealed class ActionExecutor
{
    private readonly ISystemMediaController mediaController;
    private readonly ISystemVolumeController volumeController;

    public ActionExecutor()
        : this(SystemMediaControllerFactory.Create(), SystemVolumeControllerFactory.Create())
    {
    }

    public ActionExecutor(ISystemMediaController mediaController, ISystemVolumeController volumeController)
    {
        this.mediaController = mediaController;
        this.volumeController = volumeController;
    }

    public Task<ActionExecutionResult> ExecuteAsync(
        ActionExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return context.Binding.Action switch
        {
            "media.playPause" => mediaController.PlayPauseAsync(cancellationToken),
            "system.volume" => SetSystemVolumeAsync(context.Value, cancellationToken),
            "log" => Task.FromResult(ActionExecutionResult.Ok(context.Binding.Parameters.GetValueOrDefault("message", "Log action matched"))),
            "" => Task.FromResult(ActionExecutionResult.Failed("Action is empty")),
            _ => Task.FromResult(ActionExecutionResult.Failed($"Unsupported action: {context.Binding.Action}"))
        };
    }

    private Task<ActionExecutionResult> SetSystemVolumeAsync(
        string? sliderValue,
        CancellationToken cancellationToken)
    {
        if (!int.TryParse(sliderValue, out var rawValue))
        {
            return Task.FromResult(ActionExecutionResult.Failed("Volume slider value is missing or invalid"));
        }

        var clampedRawValue = Math.Clamp(rawValue, 0, 4095);
        var percent = (int)Math.Round(clampedRawValue / 4095.0 * 100);
        return volumeController.SetMasterVolumeAsync(percent, cancellationToken);
    }
}
