namespace MakroPad.Core.Actions;

[Flags]
public enum InputKinds
{
    Button = 1,
    Slider = 2,
    Any = Button | Slider
}

public sealed record ParameterDefinition(string Name, bool Required, string Hint, string Example);

/// <summary>Describes an action for validation, editors and documentation.</summary>
public sealed record ActionDefinition(
    string Id,
    string DisplayName,
    InputKinds Inputs,
    string Description,
    IReadOnlyList<ParameterDefinition> Parameters)
{
    public ParameterDefinition? FindParameter(string name)
        => Parameters.FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
}

/// <summary>
/// The single list of supported actions. Handlers (e.g. in MakroPad.Windows) implement these ids;
/// validation and future editors read the metadata from here.
/// </summary>
public static class ActionCatalog
{
    private static readonly ParameterDefinition[] None = [];

    public static IReadOnlyList<ActionDefinition> All { get; } =
    [
        new("media.playPause", "Play/Pause", InputKinds.Button, "Medientaste Play/Pause", None),
        new("media.nextTrack", "Nächster Titel", InputKinds.Button, "Medientaste Weiter", None),
        new("media.previousTrack", "Vorheriger Titel", InputKinds.Button, "Medientaste Zurück", None),
        new("media.stop", "Stopp", InputKinds.Button, "Medientaste Stopp", None),
        new("media.mute", "Stumm", InputKinds.Button, "Systemton stumm schalten", None),
        new("media.volumeUp", "Lauter", InputKinds.Button, "Lautstärke-Taste +", None),
        new("media.volumeDown", "Leiser", InputKinds.Button, "Lautstärke-Taste −", None),
        new("system.volume", "Systemlautstärke", InputKinds.Slider, "Setzt die Lautstärke des Standard-Ausgabegeräts", None),
        new("app.volume", "Programm-Lautstärke", InputKinds.Slider, "Setzt die Lautstärke aller Audio-Sessions eines Programms",
        [
            new("process", true, "Prozessname ohne .exe", "Spotify"),
            new("label", false, "Anzeigename auf dem OLED", "Musik")
        ]),
        new("keyboard.shortcut", "Tastenkombination", InputKinds.Button, "Sendet eine Tastenkombination an das aktive Fenster",
        [
            new("keys", true, "Tasten mit + verbunden: Ctrl, Shift, Alt, Win, A-Z, 0-9, F1-F24, Enter, Esc, Tab, Space, Pfeiltasten …", "Ctrl+Shift+Esc"),
            new("label", false, "Anzeigename auf dem OLED", "Task-Manager")
        ]),
        new("system.openUrl", "Webseite öffnen", InputKinds.Button, "Öffnet eine http(s)-Adresse im Standardbrowser",
        [
            new("url", true, "Absolute http- oder https-Adresse", "https://example.com"),
            new("label", false, "Anzeigename auf dem OLED", "Example")
        ]),
        new("system.openApp", "Programm starten", InputKinds.Button, "Startet ein Programm oder öffnet eine Datei",
        [
            new("path", true, "Programm, Datei oder Befehl im PATH", "notepad.exe"),
            new("args", false, "Argumente", ""),
            new("label", false, "Anzeigename auf dem OLED", "Editor")
        ]),
        new("profile.next", "Nächstes Profil", InputKinds.Button, "Wechselt zum nächsten Profil in der Liste", None),
        new("profile.set", "Profil wählen", InputKinds.Button, "Wechselt zu einem bestimmten Profil",
        [
            new("name", true, "Name des Profils", "Gaming")
        ])
    ];

    public static ActionDefinition? Find(string actionId)
        => All.FirstOrDefault(definition => string.Equals(definition.Id, actionId, StringComparison.OrdinalIgnoreCase));
}
