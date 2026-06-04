using System;
using System.Collections.Generic;
using System.Linq;

namespace MakroPad.App.Services;

public sealed class ActionDefinitionCatalog
{
    private static readonly IReadOnlyList<ActionDefinition> Definitions =
    [
        new("media.playPause", "Media Play/Pause", "No parameters required.", "", []),
        new("media.nextTrack", "Next media track", "No parameters required.", "", []),
        new("media.previousTrack", "Previous media track", "No parameters required.", "", []),
        new("media.stop", "Stop media playback", "No parameters required.", "", []),
        new("media.volumeUp", "Increase system volume", "No parameters required.", "", []),
        new("media.volumeDown", "Decrease system volume", "No parameters required.", "", []),
        new("media.mute", "Toggle system mute", "No parameters required.", "", []),
        new("system.volume", "Map slider value to system volume", "No parameters required. This action uses the incoming slider value.", "", []),
        new("app.volume", "Map slider value to one app's volume", "Required parameter: process. Example: process=Spotify. This action uses the incoming slider value.", "process=Spotify", ["process"], "process", "Process"),
        new("system.openUrl", "Open a URL with the default browser", "Required parameter: url. Example: url=https://example.com", "url=https://example.com", ["url"], "url", "URL"),
        new("system.openApp", "Open an app or file through the OS shell", "Required parameter: path. Example: path=notepad.exe", "path=notepad.exe", ["path"], "path", "App or file"),
        new("keyboard.shortcut", "Send a keyboard shortcut", "Required parameter: keys. Supported examples: Ctrl+C, Ctrl+Shift+Esc, Alt+Tab, Win+E, F1.", "keys=Ctrl+Shift+Esc", ["keys"], "keys", "Shortcut"),
        new("log", "Write a message to the app log", "Optional parameter: message. Example: message=Hello MakroPad", "message=Hello MakroPad", [], "message", "Message")
    ];

    private readonly IReadOnlyDictionary<string, ActionDefinition> definitionsById =
        Definitions.ToDictionary(definition => definition.Id, StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<ActionDefinition> All => Definitions;

    public IReadOnlyList<string> ActionIds { get; } =
        Definitions.Select(definition => definition.Id).ToArray();

    public bool TryGet(string? action, out ActionDefinition definition)
    {
        return definitionsById.TryGetValue(action?.Trim() ?? "", out definition!);
    }
}
