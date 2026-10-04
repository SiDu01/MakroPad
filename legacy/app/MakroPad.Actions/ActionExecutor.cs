namespace MakroPad.Actions;

public sealed class ActionExecutor
{
    private readonly ISystemMediaController mediaController;
    private readonly ISystemVolumeController volumeController;
    private readonly IApplicationVolumeController applicationVolumeController;
    private readonly IKeyboardController keyboardController;
    private readonly ISystemLauncher systemLauncher;

    public ActionExecutor()
        : this(
            SystemMediaControllerFactory.Create(),
            SystemVolumeControllerFactory.Create(),
            ApplicationVolumeControllerFactory.Create(),
            KeyboardControllerFactory.Create(),
            SystemLauncherFactory.Create())
    {
    }

    public ActionExecutor(ISystemMediaController mediaController, ISystemVolumeController volumeController)
        : this(
            mediaController,
            volumeController,
            ApplicationVolumeControllerFactory.Create(),
            KeyboardControllerFactory.Create(),
            SystemLauncherFactory.Create())
    {
    }

    public ActionExecutor(
        ISystemMediaController mediaController,
        ISystemVolumeController volumeController,
        IKeyboardController keyboardController,
        ISystemLauncher systemLauncher)
        : this(
            mediaController,
            volumeController,
            ApplicationVolumeControllerFactory.Create(),
            keyboardController,
            systemLauncher)
    {
    }

    public ActionExecutor(
        ISystemMediaController mediaController,
        ISystemVolumeController volumeController,
        IApplicationVolumeController applicationVolumeController,
        IKeyboardController keyboardController,
        ISystemLauncher systemLauncher)
    {
        this.mediaController = mediaController;
        this.volumeController = volumeController;
        this.applicationVolumeController = applicationVolumeController;
        this.keyboardController = keyboardController;
        this.systemLauncher = systemLauncher;
    }

    public Task<ActionExecutionResult> ExecuteAsync(
        ActionExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return context.Binding.Action switch
        {
            "media.playPause" => mediaController.PlayPauseAsync(cancellationToken),
            "media.nextTrack" => mediaController.NextTrackAsync(cancellationToken),
            "media.previousTrack" => mediaController.PreviousTrackAsync(cancellationToken),
            "media.stop" => mediaController.StopAsync(cancellationToken),
            "media.volumeUp" => mediaController.VolumeUpAsync(cancellationToken),
            "media.volumeDown" => mediaController.VolumeDownAsync(cancellationToken),
            "media.mute" => mediaController.MuteAsync(cancellationToken),
            "system.volume" => SetSystemVolumeAsync(context.Value, cancellationToken),
            "app.volume" => SetApplicationVolumeAsync(context.Binding.Parameters, context.Value, cancellationToken),
            "system.openUrl" => OpenUrlAsync(context.Binding.Parameters, cancellationToken),
            "system.openApp" => OpenAppAsync(context.Binding.Parameters, cancellationToken),
            "keyboard.shortcut" => SendShortcutAsync(context.Binding.Parameters, cancellationToken),
            "log" => Task.FromResult(ActionExecutionResult.Ok(context.Binding.Parameters.GetValueOrDefault("message", "Log action matched"))),
            "" => Task.FromResult(ActionExecutionResult.Failed("Action is empty")),
            _ => Task.FromResult(ActionExecutionResult.Failed($"Unsupported action: {context.Binding.Action}"))
        };
    }

    private Task<ActionExecutionResult> SetSystemVolumeAsync(
        string? sliderValue,
        CancellationToken cancellationToken)
    {
        if (!TryMapSliderValueToPercent(sliderValue, out var percent))
        {
            return Task.FromResult(ActionExecutionResult.Failed("Volume slider value is missing or invalid"));
        }

        return volumeController.SetMasterVolumeAsync(percent, cancellationToken);
    }

    private Task<ActionExecutionResult> SetApplicationVolumeAsync(
        IReadOnlyDictionary<string, string> parameters,
        string? sliderValue,
        CancellationToken cancellationToken)
    {
        if (!TryMapSliderValueToPercent(sliderValue, out var percent))
        {
            return Task.FromResult(ActionExecutionResult.Failed("Volume slider value is missing or invalid"));
        }

        var processName = GetFirstParameter(parameters, "process", "processName", "app");
        return string.IsNullOrWhiteSpace(processName)
            ? Task.FromResult(ActionExecutionResult.Failed("App volume process parameter is required"))
            : applicationVolumeController.SetApplicationVolumeAsync(processName, percent, cancellationToken);
    }

    private Task<ActionExecutionResult> SendShortcutAsync(
        IReadOnlyDictionary<string, string> parameters,
        CancellationToken cancellationToken)
    {
        var shortcut = GetFirstParameter(parameters, "keys", "shortcut");
        return string.IsNullOrWhiteSpace(shortcut)
            ? Task.FromResult(ActionExecutionResult.Failed("Shortcut parameter is required"))
            : keyboardController.SendShortcutAsync(shortcut, cancellationToken);
    }

    private Task<ActionExecutionResult> OpenUrlAsync(
        IReadOnlyDictionary<string, string> parameters,
        CancellationToken cancellationToken)
    {
        var url = GetFirstParameter(parameters, "url", "target");
        return string.IsNullOrWhiteSpace(url)
            ? Task.FromResult(ActionExecutionResult.Failed("URL parameter is required"))
            : systemLauncher.OpenUrlAsync(url, cancellationToken);
    }

    private Task<ActionExecutionResult> OpenAppAsync(
        IReadOnlyDictionary<string, string> parameters,
        CancellationToken cancellationToken)
    {
        var target = GetFirstParameter(parameters, "path", "target", "app");
        return string.IsNullOrWhiteSpace(target)
            ? Task.FromResult(ActionExecutionResult.Failed("App target parameter is required"))
            : systemLauncher.OpenAppAsync(target, cancellationToken);
    }

    private static string? GetFirstParameter(
        IReadOnlyDictionary<string, string> parameters,
        params string[] keys)
    {
        foreach (var key in keys)
        {
            if (parameters.TryGetValue(key, out var value))
            {
                return value;
            }
        }

        return null;
    }

    private static bool TryMapSliderValueToPercent(string? sliderValue, out int percent)
    {
        percent = 0;
        if (!int.TryParse(sliderValue, out var rawValue))
        {
            return false;
        }

        var clampedRawValue = Math.Clamp(rawValue, 0, 4095);
        percent = (int)Math.Round(clampedRawValue / 4095.0 * 100);
        return true;
    }
}
