using MakroPad.Core.Actions;

namespace MakroPad.Windows;

/// <summary>Sends a Windows media/volume key, e.g. <c>media.playPause</c>.</summary>
public sealed class MediaKeyAction(string actionId, ushort virtualKey, string label) : IActionHandler
{
    public string ActionId { get; } = actionId;

    public Task<ActionResult> ExecuteAsync(ActionContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        NativeInput.TapExtendedKey(virtualKey);
        return Task.FromResult(ActionResult.Ok(label));
    }

    public static IReadOnlyList<MediaKeyAction> All { get; } =
    [
        new("media.playPause", 0xB3, "Play/Pause"),
        new("media.nextTrack", 0xB0, "Nächster Titel"),
        new("media.previousTrack", 0xB1, "Vorheriger Titel"),
        new("media.stop", 0xB2, "Stopp"),
        new("media.mute", 0xAD, "Stumm"),
        new("media.volumeDown", 0xAE, "Leiser"),
        new("media.volumeUp", 0xAF, "Lauter")
    ];
}
